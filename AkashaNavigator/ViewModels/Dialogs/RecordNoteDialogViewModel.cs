using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.PioneerNote;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 目录树项模型（用于 TreeView 绑定）
    /// </summary>
    public class FolderTreeItem
    {
        /// <summary>
        /// 目录 ID（null 表示根目录）
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 目录名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 目录图标
        /// </summary>
        public string Icon { get; set; } = "📁";

        /// <summary>
        /// 是否为根目录
        /// </summary>
        public bool IsRoot { get; set; }

        /// <summary>
        /// 子目录
        /// </summary>
        public ObservableCollection<FolderTreeItem> Children { get; set; } = new();
    }

    /// <summary>
    /// 记录笔记对话框的 ViewModel
    /// 用于创建新的笔记项，支持选择目录和新建目录
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class RecordNoteDialogViewModel : ObservableObject
    {
        #region Fields

        private readonly IPioneerNoteService _pioneerNoteService;
        private string? _selectedFolderId;

        #endregion

        #region Observable Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消（自动生成 DialogResult 属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool? _dialogResult;

        /// <summary>
        /// 笔记标题（自动生成 Title 属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _title = string.Empty;

        /// <summary>
        /// URL（自动生成 Url 属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _url = string.Empty;

        /// <summary>
        /// 错误消息（自动生成 ErrorMessage 属性和通知）
        /// </summary>
        [ObservableProperty]
        private string? _errorMessage;

        /// <summary>
        /// 是否显示错误消息（自动生成 HasError 属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _hasError;

        #endregion

        #region Properties

        /// <summary>
        /// 创建的笔记项（确认后可用）
        /// </summary>
        public NoteItem? CreatedNote { get; private set; }

        /// <summary>
        /// 目录树集合
        /// </summary>
        public ObservableCollection<FolderTreeItem> FolderTreeItems { get; } = new();

        /// <summary>
        /// 是否显示新建目录面板
        /// </summary>
        [ObservableProperty]
        private bool _showNewFolderPanel;

        /// <summary>
        /// 新建目录名称
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmNewFolderCommand))]
        private string _newFolderName = string.Empty;

        #endregion

        #region Constructor

        /// <summary>
        /// 构造函数 - 只接收服务依赖
        /// </summary>
        public RecordNoteDialogViewModel(IPioneerNoteService pioneerNoteService)
        {
            _pioneerNoteService = pioneerNoteService ?? throw new ArgumentNullException(nameof(pioneerNoteService));
        }

        /// <summary>
        /// 初始化方法 - 接收运行时参数
        /// </summary>
        public void Initialize(string url, string defaultTitle)
        {
            Url = url ?? string.Empty;
            Title = defaultTitle ?? string.Empty;

            LoadFolderTree();
        }

        #endregion

        #region Folder Tree

        /// <summary>
        /// 加载笔记目录树
        /// </summary>
        public void LoadFolderTree()
        {
            FolderTreeItems.Clear();

            // 添加根目录选项（始终显示在顶部）
            var rootItem = new FolderTreeItem
            {
                Id = null, // null 表示根目录
                Name = "根目录",
                Icon = "🏠",
                IsRoot = true,
                Children = new ObservableCollection<FolderTreeItem>()
            };

            // 获取所有顶级目录
            var folders = _pioneerNoteService.GetFoldersByParent(null);

            // 递归构建目录树，作为根目录的子项
            foreach (var folder in folders)
            {
                var treeItem = BuildFolderTreeItem(folder);
                rootItem.Children.Add(treeItem);
            }

            FolderTreeItems.Add(rootItem);

            // 默认选中根目录
            _selectedFolderId = null;
        }

        /// <summary>
        /// 递归构建目录树项
        /// </summary>
        private FolderTreeItem BuildFolderTreeItem(NoteFolder folder)
        {
            var item = new FolderTreeItem
            {
                Id = folder.Id,
                Name = folder.Name,
                Icon = folder.Icon ?? "📁",
                Children = new ObservableCollection<FolderTreeItem>()
            };

            // 获取子目录
            var childFolders = _pioneerNoteService.GetFoldersByParent(folder.Id);
            foreach (var childFolder in childFolders)
            {
                var childItem = BuildFolderTreeItem(childFolder);
                item.Children.Add(childItem);
            }

            return item;
        }

        /// <summary>
        /// 目录树选择变化
        /// </summary>
        public void OnFolderSelected(FolderTreeItem? selectedItem)
        {
            if (selectedItem != null)
            {
                // 根目录的 Id 为 null，其他目录使用实际 Id
                _selectedFolderId = selectedItem.Id;
            }
            else
            {
                // 没有选中任何项时，默认记录到根目录
                _selectedFolderId = null;
            }
        }

        /// <summary>
        /// 清除目录选中状态（由 Code-behind 调用）
        /// </summary>
        public void ClearFolderSelection()
        {
            _selectedFolderId = null;
        }

        #endregion

        #region Commands

        /// <summary>
        /// 确定命令（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!ValidateInput())
            {
                return;
            }

            try
            {
                // 创建笔记
                var title = Title.Trim();
                var url = Url.Trim();
                CreatedNote = _pioneerNoteService.RecordNote(url, title, _selectedFolderId);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        /// <summary>
        /// 是否可以确认（标题和 URL 不为空）
        /// </summary>
        private bool CanConfirm() => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Url);

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
        }

        /// <summary>
        /// 关闭命令（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
        }

        /// <summary>
        /// 显示新建目录面板命令（自动生成 ShowNewFolderCommand）
        /// </summary>
        [RelayCommand]
        private void ShowNewFolder()
        {
            ShowNewFolderPanel = true;
            NewFolderName = string.Empty;
        }

        /// <summary>
        /// 隐藏新建目录面板命令（自动生成 HideNewFolderCommand）
        /// </summary>
        [RelayCommand]
        private void HideNewFolder()
        {
            ShowNewFolderPanel = false;
            NewFolderName = string.Empty;
        }

        /// <summary>
        /// 确认新建目录命令（自动生成 ConfirmNewFolderCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConfirmNewFolder))]
        private void ConfirmNewFolder()
        {
            var folderName = NewFolderName.Trim();
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            try
            {
                // 在当前选中的目录下创建新目录
                var newFolder = _pioneerNoteService.CreateFolder(folderName, _selectedFolderId);

                // 刷新目录树
                LoadFolderTree();

                // 隐藏新建面板
                ShowNewFolderPanel = false;
                NewFolderName = string.Empty;

                // 设置新创建的目录为选中状态（返回 ID 供 Code-behind 使用）
                NewFolderCreatedId = newFolder.Id;
            }
            catch (Exception ex)
            {
                ShowError($"创建目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否可以确认新建目录（名称不为空）
        /// </summary>
        private bool CanConfirmNewFolder() => !string.IsNullOrWhiteSpace(NewFolderName);

        /// <summary>
        /// 编辑文件夹命令（自动生成 EditFolderCommand）
        /// </summary>
        public void EditFolder(FolderTreeItem selectedItem)
        {
            if (selectedItem == null || selectedItem.IsRoot)
            {
                return;
            }

            // 这个操作需要通过 IDialogFactory 打开对话框
            // 由 Code-behind 处理
            FolderToEdit = selectedItem;
        }

        /// <summary>
        /// 删除文件夹命令（自动生成 DeleteFolderCommand）
        /// </summary>
        public void DeleteFolder(FolderTreeItem selectedItem)
        {
            if (selectedItem == null || selectedItem.IsRoot)
            {
                return;
            }

            // 这个操作需要通过 IDialogFactory 打开确认对话框
            // 由 Code-behind 处理
            FolderToDelete = selectedItem;
        }

        /// <summary>
        /// 执行删除文件夹操作（由 Code-behind 在确认后调用）
        /// </summary>
        public void ExecuteDeleteFolder()
        {
            if (FolderToDelete == null || FolderToDelete.IsRoot)
            {
                return;
            }

            try
            {
                _pioneerNoteService.DeleteFolder(FolderToDelete.Id!, cascade: true);
                LoadFolderTree();
                _selectedFolderId = null;
                FolderToDelete = null;
            }
            catch (Exception ex)
            {
                ShowError($"删除目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行编辑文件夹操作（由 Code-behind 在输入新名称后调用）
        /// </summary>
        public void ExecuteEditFolder(string newName)
        {
            if (FolderToEdit == null || FolderToEdit.IsRoot)
            {
                return;
            }

            try
            {
                _pioneerNoteService.UpdateFolder(FolderToEdit.Id!, newName);
                LoadFolderTree();
                FolderToEdit = null;
            }
            catch (Exception ex)
            {
                ShowError($"编辑目录失败: {ex.Message}");
            }
        }

        #endregion

        #region Public Helper Properties

        /// <summary>
        /// 待编辑的文件夹（由 Code-behind 读取后打开对话框）
        /// </summary>
        [ObservableProperty]
        private FolderTreeItem? _folderToEdit;

        /// <summary>
        /// 待删除的文件夹（由 Code-behind 读取后显示确认对话框）
        /// </summary>
        [ObservableProperty]
        private FolderTreeItem? _folderToDelete;

        /// <summary>
        /// 新创建的目录 ID（供 Code-behind 选中用）
        /// </summary>
        public string? NewFolderCreatedId { get; private set; }

        #endregion

        #region Validation

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            ClearError();

            var title = Title.Trim();
            var url = Url.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                ShowError("笔记标题不能为空");
                return false;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowError("URL 不能为空");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        /// <summary>
        /// 清除错误消息
        /// </summary>
        public void ClearError()
        {
            ErrorMessage = null;
            HasError = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 设置当前 URL（从 PlayerWindow 获取）
        /// </summary>
        public void SetCurrentUrl(string? currentUrl)
        {
            if (!string.IsNullOrWhiteSpace(currentUrl))
            {
                Url = currentUrl;
            }
        }

        #endregion
    }
}
