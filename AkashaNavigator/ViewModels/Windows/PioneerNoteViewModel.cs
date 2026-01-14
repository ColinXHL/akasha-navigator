using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.PioneerNote;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 开荒笔记窗口 ViewModel - 混合架构
/// </summary>
public partial class PioneerNoteViewModel : ObservableObject
{
    private readonly IPioneerNoteService _pioneerNoteService;
    private ObservableCollection<NoteTreeNode> _treeNodes = new();

    /// <summary>
    /// 笔记树
    /// </summary>
    public ObservableCollection<NoteTreeNode> TreeNodes
    {
        get => _treeNodes;
    private
        set => SetProperty(ref _treeNodes, value);
    }

    /// <summary>
    /// 搜索关键词（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>
    /// 是否为空（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>
    /// 空状态提示文本（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _emptyHintText = "暂无笔记内容";

    /// <summary>
    /// 排序按钮文本（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _sortButtonText = "↓ 最新";

    /// <summary>
    /// 选择笔记项事件（由 Code-behind 订阅）
    /// </summary>
    public event EventHandler<NoteTreeNode?>? NodeSelected;

    /// <summary>
    /// 请求显示编辑对话框事件
    /// </summary>
    public event EventHandler<NoteTreeNode?>? ShowEditDialogRequested;

    /// <summary>
    /// 请求显示删除确认对话框事件
    /// </summary>
    public event EventHandler<NoteTreeNode?>? ShowDeleteConfirmRequested;

    /// <summary>
    /// 请求显示新建目录对话框事件
    /// </summary>
    public event EventHandler<string?>? ShowNewFolderDialogRequested;

    /// <summary>
    /// 请求显示移动对话框事件
    /// </summary>
    public event EventHandler<NoteTreeNode?>? ShowMoveDialogRequested;

    /// <summary>
    /// 请求显示记录笔记对话框事件
    /// </summary>
    public event EventHandler? ShowRecordNoteDialogRequested;

    /// <summary>
    /// 操作失败事件（用于通知 View 显示错误信息）
    /// </summary>
    public event EventHandler<string>? OperationFailed;

    public PioneerNoteViewModel(IPioneerNoteService pioneerNoteService)
    {
        _pioneerNoteService = pioneerNoteService ?? throw new ArgumentNullException(nameof(pioneerNoteService));
        LoadNoteTree();
        UpdateSortButton();
    }

    /// <summary>
    /// 搜索关键词变化时重新加载（自动生成的方法）
    /// </summary>
    partial void OnSearchKeywordChanged(string value)
    {
        LoadNoteTree();
    }

    /// <summary>
    /// 加载笔记树
    /// </summary>
    public void LoadNoteTree()
    {
        TreeNodes.Clear();

        var noteData = _pioneerNoteService.GetNoteTree();
        var sortDirection = noteData.SortOrder;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            LoadSearchResults(noteData, sortDirection);
            IsEmpty = TreeNodes.Count == 0;
            return;
        }

        // 构建树形结构（保留原有逻辑）
        var rootFolders = noteData.Folders.Where(f => f.ParentId == null).ToList();

        rootFolders = sortDirection == SortDirection.Ascending
                          ? rootFolders.OrderBy(f => f.CreatedTime).ToList()
                          : rootFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var folder in rootFolders)
        {
            var folderNode = BuildFolderNode(folder, noteData, sortDirection);
            TreeNodes.Add(folderNode);
        }

        var rootItems = noteData.Items.Where(i => i.FolderId == null).ToList();

        rootItems = SortItems(rootItems, sortDirection);

        foreach (var item in rootItems)
        {
            var itemNode = BuildItemNode(item);
            TreeNodes.Add(itemNode);
        }

        IsEmpty = TreeNodes.Count == 0;
    }

    /// <summary>
    /// 加载搜索结果（以树形结构展现，只显示匹配的目录和笔记项）
    /// </summary>
    private void LoadSearchResults(PioneerNoteData noteData, SortDirection sortDirection)
    {
        var searchResults = _pioneerNoteService.SearchNotes(SearchKeyword);

        // 收集所有匹配项的目录 ID
        var matchedFolderIds = new HashSet<string>();
        foreach (var item in searchResults)
        {
            if (!string.IsNullOrEmpty(item.FolderId))
            {
                // 添加该目录及其所有父目录
                var folderId = item.FolderId;
                while (!string.IsNullOrEmpty(folderId))
                {
                    matchedFolderIds.Add(folderId);
                    var folder = noteData.Folders.FirstOrDefault(f => f.Id == folderId);
                    folderId = folder?.ParentId;
                }
            }
        }

        // 构建树形结构，只包含匹配的目录（按时间排序）
        var rootFolders = noteData.Folders.Where(f => f.ParentId == null && matchedFolderIds.Contains(f.Id)).ToList();
        rootFolders = sortDirection == SortDirection.Ascending
                          ? rootFolders.OrderBy(f => f.CreatedTime).ToList()
                          : rootFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var folder in rootFolders)
        {
            var folderNode = BuildSearchFolderNode(folder, noteData, sortDirection, searchResults, matchedFolderIds);
            if (folderNode.Children?.Count > 0)
            {
                TreeNodes.Add(folderNode);
            }
        }

        // 添加根目录下的匹配笔记项
        var rootItems = searchResults.Where(i => i.FolderId == null).ToList();
        rootItems = SortItems(rootItems, sortDirection);

        foreach (var item in rootItems)
        {
            var itemNode = BuildItemNode(item);
            TreeNodes.Add(itemNode);
        }

        // 更新空状态提示
        if (TreeNodes.Count == 0 && !string.IsNullOrWhiteSpace(SearchKeyword))
        {
            EmptyHintText = "未找到匹配的笔记";
        }
        else
        {
            EmptyHintText = "暂无笔记内容";
        }
    }

    /// <summary>
    /// 构建搜索结果的目录节点（只包含匹配的子项）
    /// </summary>
    private NoteTreeNode BuildSearchFolderNode(NoteFolder folder, PioneerNoteData noteData, SortDirection sortDirection,
                                               List<NoteItem> searchResults, HashSet<string> matchedFolderIds)
    {
        var node = new NoteTreeNode { Id = folder.Id,
                                      Title = folder.Name,
                                      Icon = folder.Icon ?? "📁",
                                      IsFolder = true,
                                      RecordedTime = folder.CreatedTime,
                                      Children = new ObservableCollection<NoteTreeNode>() };

        // 添加匹配的子目录（按时间排序）
        var childFolders =
            noteData.Folders.Where(f => f.ParentId == folder.Id && matchedFolderIds.Contains(f.Id)).ToList();
        childFolders = sortDirection == SortDirection.Ascending
                           ? childFolders.OrderBy(f => f.CreatedTime).ToList()
                           : childFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var childFolder in childFolders)
        {
            var childNode =
                BuildSearchFolderNode(childFolder, noteData, sortDirection, searchResults, matchedFolderIds);
            if (childNode.Children?.Count > 0)
            {
                node.Children.Add(childNode);
            }
        }

        // 添加目录下匹配的笔记项
        var items = searchResults.Where(i => i.FolderId == folder.Id).ToList();
        items = SortItems(items, sortDirection);

        foreach (var item in items)
        {
            var itemNode = BuildItemNode(item);
            node.Children.Add(itemNode);
        }

        return node;
    }

    /// <summary>
    /// 构建目录节点
    /// </summary>
    private NoteTreeNode BuildFolderNode(NoteFolder folder, PioneerNoteData noteData, SortDirection sortDirection)
    {
        var node = new NoteTreeNode { Id = folder.Id,
                                      Title = folder.Name,
                                      Icon = folder.Icon ?? "📁",
                                      IsFolder = true,
                                      RecordedTime = folder.CreatedTime,
                                      Children = new ObservableCollection<NoteTreeNode>() };

        // 添加子目录（按时间排序）
        var childFolders = noteData.Folders.Where(f => f.ParentId == folder.Id).ToList();
        childFolders = sortDirection == SortDirection.Ascending
                           ? childFolders.OrderBy(f => f.CreatedTime).ToList()
                           : childFolders.OrderByDescending(f => f.CreatedTime).ToList();

        foreach (var childFolder in childFolders)
        {
            var childNode = BuildFolderNode(childFolder, noteData, sortDirection);
            node.Children.Add(childNode);
        }

        // 添加目录下的笔记项
        var items = noteData.Items.Where(i => i.FolderId == folder.Id).ToList();
        items = SortItems(items, sortDirection);

        foreach (var item in items)
        {
            var itemNode = BuildItemNode(item);
            node.Children.Add(itemNode);
        }

        return node;
    }

    /// <summary>
    /// 构建笔记项节点
    /// </summary>
    private NoteTreeNode BuildItemNode(NoteItem item)
    {
        return new NoteTreeNode { Id = item.Id,
                                  Title = item.Title,
                                  Url = item.Url,
                                  Icon = "🔗",
                                  IsFolder = false,
                                  RecordedTime = item.RecordedTime,
                                  FolderId = item.FolderId };
    }

    /// <summary>
    /// 排序笔记项
    /// </summary>
    private List<NoteItem> SortItems(List<NoteItem> items, SortDirection direction)
    {
        return direction == SortDirection.Ascending ? items.OrderBy(i => i.RecordedTime).ToList()
                                                    : items.OrderByDescending(i => i.RecordedTime).ToList();
    }

    /// <summary>
    /// 切换排序（自动生成 ToggleSortCommand）
    /// </summary>
    [RelayCommand]
    private void ToggleSort()
    {
        _pioneerNoteService.ToggleSortOrder();
        UpdateSortButton();
        LoadNoteTree();
    }

    /// <summary>
    /// 更新排序按钮文本
    /// </summary>
    private void UpdateSortButton()
    {
        var sortOrder = _pioneerNoteService.CurrentSortOrder;
        SortButtonText = sortOrder == SortDirection.Descending ? "↓ 最新" : "↑ 最早";
    }

    /// <summary>
    /// 新建目录（自动生成 NewFolderCommand）
    /// </summary>
    [RelayCommand]
    private void NewFolder(string? parentId = null)
    {
        ShowNewFolderDialogRequested?.Invoke(this, parentId);
    }

    /// <summary>
    /// 记录笔记（自动生成 RecordNoteCommand）
    /// </summary>
    [RelayCommand]
    private void RecordNote()
    {
        // 通过事件通知 Code-behind 显示对话框
        ShowRecordNoteDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 编辑节点（自动生成 EditNodeCommand）
    /// </summary>
    [RelayCommand]
    private void EditNode(NoteTreeNode? node)
    {
        if (node != null)
        {
            ShowEditDialogRequested?.Invoke(this, node);
        }
    }

    /// <summary>
    /// 删除节点（自动生成 DeleteNodeCommand）
    /// </summary>
    [RelayCommand]
    private void DeleteNode(NoteTreeNode? node)
    {
        if (node != null)
        {
            ShowDeleteConfirmRequested?.Invoke(this, node);
        }
    }

    /// <summary>
    /// 移动节点（自动生成 MoveNodeCommand）
    /// </summary>
    [RelayCommand]
    private void MoveNode(NoteTreeNode? node)
    {
        if (node != null)
        {
            ShowMoveDialogRequested?.Invoke(this, node);
        }
    }

    /// <summary>
    /// 选择节点（自动生成 SelectNodeCommand）
    /// </summary>
    [RelayCommand]
    private void SelectNode(NoteTreeNode? node)
    {
        NodeSelected?.Invoke(this, node);
    }

#region Service Call Methods

    /// <summary>
    /// 更新目录名称
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <param name="newName">新名称</param>
    public void UpdateFolder(string id, string newName)
    {
        try
        {
            _pioneerNoteService.UpdateFolder(id, newName);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"编辑目录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新笔记
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    /// <param name="newName">新标题</param>
    /// <param name="newUrl">新 URL（可选）</param>
    public void UpdateNote(string id, string newName, string? newUrl)
    {
        try
        {
            _pioneerNoteService.UpdateNote(id, newName, newUrl);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"编辑笔记失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除目录
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <param name="recursive">是否递归删除子内容</param>
    public void DeleteFolder(string id, bool recursive)
    {
        try
        {
            _pioneerNoteService.DeleteFolder(id, recursive);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"删除目录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除笔记
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    public void DeleteNote(string id)
    {
        try
        {
            _pioneerNoteService.DeleteNote(id);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"删除笔记失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建目录
    /// </summary>
    /// <param name="name">目录名称</param>
    /// <param name="parentId">父目录 ID（null 表示根目录）</param>
    public void CreateFolder(string name, string? parentId)
    {
        try
        {
            _pioneerNoteService.CreateFolder(name, parentId);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"创建目录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 移动笔记到指定目录
    /// </summary>
    /// <param name="noteId">笔记项 ID</param>
    /// <param name="targetFolderId">目标目录 ID（null 表示根目录）</param>
    public void MoveNote(string noteId, string? targetFolderId)
    {
        try
        {
            _pioneerNoteService.MoveNote(noteId, targetFolderId);
            LoadNoteTree();
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, $"移动笔记失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有目录列表（用于移动对话框）
    /// </summary>
    /// <returns>目录列表</returns>
    public List<NoteFolder> GetFolders()
    {
        var noteData = _pioneerNoteService.GetNoteTree();
        return noteData.Folders;
    }

#endregion
}
}
