using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.ViewModels.Windows;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// 开荒笔记管理窗口
/// 显示笔记树，支持搜索、排序、编辑和删除操作
/// </summary>
public partial class PioneerNoteWindow : AnimatedWindow
{
#region Events

    /// <summary>
    /// 选择笔记项事件（双击打开 URL）
    /// </summary>
    public event EventHandler<string>? NoteItemSelected;

#endregion

#region Fields

    private readonly PioneerNoteViewModel _viewModel;
    private readonly IDialogFactory _dialogFactory;

#endregion

#region Constructor

    public PioneerNoteWindow(PioneerNoteViewModel viewModel, IDialogFactory dialogFactory)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));

        InitializeComponent();
        DataContext = _viewModel;

        // 订阅 ViewModel 事件
        _viewModel.NodeSelected += OnNodeSelected;
        _viewModel.ShowEditDialogRequested += OnShowEditDialog;
        _viewModel.ShowDeleteConfirmRequested += OnShowDeleteConfirm;
        _viewModel.ShowNewFolderDialogRequested += OnShowNewFolderDialog;
        _viewModel.ShowMoveDialogRequested += OnShowMoveDialog;
        _viewModel.ShowRecordNoteDialogRequested += OnShowRecordNoteDialog;
        _viewModel.OperationFailed += OnOperationFailed;
    }

#endregion

#region Event Handlers - ViewModel Events

    private void OnNodeSelected(object? sender, NoteTreeNode? node)
    {
        if (node != null && !node.IsFolder && !string.IsNullOrEmpty(node.Url))
        {
            CloseWithAnimation(() => NoteItemSelected?.Invoke(this, node.Url));
        }
    }

    private void OnShowEditDialog(object? sender, NoteTreeNode? node)
    {
        if (node == null)
            return;
        ShowEditDialog(node);
    }

    private void OnShowDeleteConfirm(object? sender, NoteTreeNode? node)
    {
        if (node == null)
            return;
        ShowDeleteConfirmDialog(node);
    }

    private void OnShowNewFolderDialog(object? sender, string? parentId)
    {
        ShowNewFolderDialog(parentId);
    }

    private void OnShowMoveDialog(object? sender, NoteTreeNode? node)
    {
        if (node == null)
            return;
        ShowMoveDialog(node);
    }

    private void OnShowRecordNoteDialog(object? sender, EventArgs e)
    {
        ShowRecordNoteDialog();
    }

    /// <summary>
    /// 操作失败事件处理 - 显示错误消息框
    /// </summary>
    private void OnOperationFailed(object? sender, string message)
    {
        MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

#endregion

#region Private Methods - Dialogs

    /// <summary>
    /// 显示编辑对话框
    /// </summary>
    private void ShowEditDialog(NoteTreeNode node)
    {
        // 如果是笔记项，显示 URL 输入框
        var showUrl = !node.IsFolder;
        var editDialog =
            _dialogFactory.CreateNoteEditDialog(node.IsFolder ? "编辑目录" : "编辑笔记", node.Title, "请输入新名称：",
                                                showUrl: showUrl, isConfirmDialog: false, defaultUrl: node.Url);

        editDialog.Owner = this;
        editDialog.ShowDialog();

        if (editDialog.Result == true && !string.IsNullOrWhiteSpace(editDialog.InputText))
        {
            // 调用 ViewModel 方法处理结果（错误处理由 ViewModel 通过事件通知）
            if (node.IsFolder)
            {
                _viewModel.UpdateFolder(node.Id!, editDialog.InputText);
            }
            else
            {
                _viewModel.UpdateNote(node.Id!, editDialog.InputText, editDialog.UrlText);
            }
        }
    }

    /// <summary>
    /// 显示删除确认对话框
    /// </summary>
    private void ShowDeleteConfirmDialog(NoteTreeNode node)
    {
        var message = node.IsFolder ? $"确定要删除目录 \"{node.Title}\" 及其所有内容吗？此操作不可撤销。"
                                    : $"确定要删除笔记 \"{node.Title}\" 吗？此操作不可撤销。";

        // 使用自定义对话框而不是系统 MessageBox
        // 参数顺序: title, defaultValue, prompt, showUrl, isConfirmDialog
        var confirmDialog = _dialogFactory.CreateNoteEditDialog("确认删除", "", message, false, true);
        confirmDialog.Owner = this;
        confirmDialog.ShowDialog();

        if (confirmDialog.Result == true)
        {
            // 调用 ViewModel 方法处理结果（错误处理由 ViewModel 通过事件通知）
            if (node.IsFolder)
            {
                _viewModel.DeleteFolder(node.Id!, true);
            }
            else
            {
                _viewModel.DeleteNote(node.Id!);
            }
        }
    }

    /// <summary>
    /// 显示新建目录对话框
    /// </summary>
    private void ShowNewFolderDialog(string? parentId = null)
    {
        var editDialog = _dialogFactory.CreateNoteEditDialog("新建目录", "", "请输入目录名称：");

        editDialog.Owner = this;
        editDialog.ShowDialog();

        if (editDialog.Result == true && !string.IsNullOrWhiteSpace(editDialog.InputText))
        {
            // 调用 ViewModel 方法处理结果（错误处理由 ViewModel 通过事件通知）
            _viewModel.CreateFolder(editDialog.InputText, parentId);
        }
    }

    /// <summary>
    /// 显示移动对话框
    /// </summary>
    private void ShowMoveDialog(NoteTreeNode node)
    {
        if (node.IsFolder)
            return;

        // 从 ViewModel 获取所有目录用于选择
        var folders = _viewModel.GetFolders();

        // 创建目录选择对话框（使用 DialogFactory）
        var moveDialog = _dialogFactory.CreateNoteMoveDialog(folders, node.FolderId);
        moveDialog.Owner = this;
        moveDialog.ShowDialog();

        if (moveDialog.Result)
        {
            // 调用 ViewModel 方法处理结果（错误处理由 ViewModel 通过事件通知）
            _viewModel.MoveNote(node.Id!, moveDialog.SelectedFolderId);
        }
    }

    /// <summary>
    /// 显示记录笔记对话框
    /// </summary>
    private void ShowRecordNoteDialog()
    {
        // 使用 IDialogFactory 创建对话框
        var noteDialog = _dialogFactory.CreateRecordNoteDialog("", "");
        noteDialog.Owner = this;
        noteDialog.ShowDialog();

        if (noteDialog.Result && noteDialog.CreatedNote != null)
        {
            // 笔记已创建，刷新树
            _viewModel.LoadNoteTree();
        }
    }

    /// <summary>
    /// 删除项按钮点击
    /// </summary>
    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            // 查找对应的节点
            var node = FindNodeById(id, _viewModel.TreeNodes);
            if (node != null)
            {
                ShowDeleteConfirmDialog(node);
            }
        }
        e.Handled = true;
    }

    /// <summary>
    /// 根据 ID 查找节点
    /// </summary>
    private NoteTreeNode? FindNodeById(string id, IEnumerable<NoteTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
                return node;

            if (node.Children != null && node.Children.Count > 0)
            {
                var found = FindNodeById(id, node.Children);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

#endregion

#region Event Handlers - UI

    /// <summary>
    /// 笔记树双击事件
    /// </summary>
    private void NoteTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NoteTree.SelectedItem is NoteTreeNode node && !node.IsFolder && !string.IsNullOrEmpty(node.Url))
        {
            _viewModel.SelectNodeCommand.Execute(node);
        }
    }

    /// <summary>
    /// 笔记树选择变化事件
    /// </summary>
    private void NoteTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // 设置右键菜单
        if (e.NewValue is NoteTreeNode node)
        {
            SetupContextMenu(node);
        }
    }

    /// <summary>
    /// 设置右键菜单
    /// </summary>
    private void SetupContextMenu(NoteTreeNode node)
    {
        var contextMenu = new ContextMenu { Style = FindResource("DarkContextMenuStyle") as Style };

        // 编辑菜单项
        var editItem = new MenuItem { Header = "✏️ 编辑", Style = FindResource("DarkMenuItemStyle") as Style };
        editItem.Click += (s, e) => _viewModel.EditNodeCommand.Execute(node);
        contextMenu.Items.Add(editItem);

        // 移动菜单项（仅笔记项可移动）
        if (!node.IsFolder)
        {
            var moveItem = new MenuItem { Header = "📂 移动到...", Style = FindResource("DarkMenuItemStyle") as Style };
            moveItem.Click += (s, e) => _viewModel.MoveNodeCommand.Execute(node);
            contextMenu.Items.Add(moveItem);
        }

        // 删除菜单项
        var deleteItem = new MenuItem { Header = "🗑️ 删除", Style = FindResource("DarkMenuItemStyle") as Style };
        deleteItem.Click += (s, e) => _viewModel.DeleteNodeCommand.Execute(node);
        contextMenu.Items.Add(deleteItem);

        // 如果是目录，添加新建子目录选项
        if (node.IsFolder)
        {
            contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(
                                                      System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)) });

            var newFolderItem =
                new MenuItem { Header = "📁 新建子目录", Style = FindResource("DarkMenuItemStyle") as Style };
            newFolderItem.Click += (s, e) => _viewModel.NewFolderCommand.Execute(node.Id);
            contextMenu.Items.Add(newFolderItem);
        }

        // 如果是笔记项，添加打开选项
        if (!node.IsFolder && !string.IsNullOrEmpty(node.Url))
        {
            contextMenu.Items.Insert(0, new Separator { Background = new System.Windows.Media.SolidColorBrush(
                                                            System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)) });

            var openItem = new MenuItem { Header = "🔗 打开", Style = FindResource("DarkMenuItemStyle") as Style };
            openItem.Click += (s, e) =>
            { CloseWithAnimation(() => NoteItemSelected?.Invoke(this, node.Url)); };
            contextMenu.Items.Insert(0, openItem);
        }

        NoteTree.ContextMenu = contextMenu;
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    /// <summary>
    /// 树容器点击事件 - 点击空白区域取消选中并使搜索框失去焦点
    /// </summary>
    private void TreeContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在 TreeViewItem 上
        var hitElement = e.OriginalSource as DependencyObject;
        while (hitElement != null)
        {
            if (hitElement is TreeViewItem)
            {
                // 点击在 TreeViewItem 上，不处理
                return;
            }
            hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
        }

        // 点击在空白区域，清除选中
        ClearTreeViewSelection();

        // 使搜索框失去焦点
        ClearSearchBoxFocus();
    }

    /// <summary>
    /// 使搜索框失去焦点
    /// </summary>
    private void ClearSearchBoxFocus()
    {
        if (SearchBox.IsFocused)
        {
            // 将焦点移到其他元素
            NoteTree.Focus();
        }
    }

    /// <summary>
    /// 内容区点击事件 - 使搜索框失去焦点
    /// </summary>
    private void ContentArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查点击是否在搜索框上
        var hitElement = e.OriginalSource as DependencyObject;
        while (hitElement != null)
        {
            if (hitElement is TextBox)
            {
                // 点击在搜索框上，不处理
                return;
            }
            hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
        }

        // 使搜索框失去焦点
        ClearSearchBoxFocus();
    }

    /// <summary>
    /// 清除 TreeView 选中状态
    /// </summary>
    private void ClearTreeViewSelection()
    {
        if (NoteTree.SelectedItem != null)
        {
            // 遍历所有 TreeViewItem 并取消选中
            ClearTreeViewItemSelection(NoteTree);
        }
    }

    /// <summary>
    /// 递归清除 TreeViewItem 选中状态
    /// </summary>
    private void ClearTreeViewItemSelection(ItemsControl parent)
    {
        foreach (var item in parent.Items)
        {
            var treeViewItem = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = false;
                if (treeViewItem.HasItems)
                {
                    ClearTreeViewItemSelection(treeViewItem);
                }
            }
        }
    }

    /// <summary>
    /// TreeViewItem 右键点击事件 - 先选中该项再显示菜单
    /// </summary>
    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 获取被右键点击的 TreeViewItem
        var treeViewItem = sender as TreeViewItem;
        if (treeViewItem != null)
        {
            // 检查点击是否在子项上
            var originalSource = e.OriginalSource as DependencyObject;
            var clickedItem = originalSource;

            // 向上遍历找到最近的 TreeViewItem
            while (clickedItem != null && clickedItem != treeViewItem)
            {
                if (clickedItem is TreeViewItem childItem && childItem != treeViewItem)
                {
                    // 点击在子项上，让子项处理
                    return;
                }
                clickedItem = System.Windows.Media.VisualTreeHelper.GetParent(clickedItem);
            }

            // 选中该项
            treeViewItem.IsSelected = true;
            treeViewItem.Focus();

            // 设置右键菜单
            if (treeViewItem.DataContext is NoteTreeNode node)
            {
                SetupContextMenu(node);
            }

            e.Handled = true;
        }
    }

#endregion
}
}
