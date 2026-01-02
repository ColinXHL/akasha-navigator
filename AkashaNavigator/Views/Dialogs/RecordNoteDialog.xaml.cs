using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 记录笔记对话框
    /// 用于创建新的笔记项，支持选择目录和新建目录
    /// </summary>
    public partial class RecordNoteDialog : AnimatedWindow
    {
        #region Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消
        /// </summary>
        public bool Result { get; private set; }

        /// <summary>
        /// 创建的笔记项（确认后可用）
        /// </summary>
        public NoteItem? CreatedNote { get; private set; }

        #endregion

        #region Fields

        private readonly RecordNoteDialogViewModel _viewModel;
        private readonly Func<PioneerNoteWindow> _pioneerNoteWindowFactory;
        private readonly IDialogFactory _dialogFactory;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建记录笔记对话框
        /// </summary>
        public RecordNoteDialog(
            RecordNoteDialogViewModel viewModel,
            IDialogFactory dialogFactory,
            Func<PioneerNoteWindow>? pioneerNoteWindowFactory = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
            _pioneerNoteWindowFactory = pioneerNoteWindowFactory ?? (() => App.Services.GetRequiredService<PioneerNoteWindow>());

            InitializeComponent();

            // 设置 DataContext
            DataContext = _viewModel;

            // 订阅 ViewModel 的对话框结果变化
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.DialogResult))
                {
                    Result = _viewModel.DialogResult == true;
                    CreatedNote = _viewModel.CreatedNote;
                    if (_viewModel.DialogResult.HasValue)
                    {
                        CloseWithAnimation();
                    }
                }
                else if (e.PropertyName == nameof(_viewModel.NewFolderCreatedId))
                {
                    // 新建目录后选中该目录
                    var newId = _viewModel.NewFolderCreatedId;
                    if (!string.IsNullOrEmpty(newId))
                    {
                        SelectFolderById(newId);
                    }
                }
                else if (e.PropertyName == nameof(_viewModel.FolderToEdit))
                {
                    // 编辑文件夹
                    HandleEditFolder();
                }
                else if (e.PropertyName == nameof(_viewModel.FolderToDelete))
                {
                    // 删除文件夹
                    HandleDeleteFolder();
                }
            };

            // 设置目录树数据源
            FolderTree.ItemsSource = _viewModel.FolderTreeItems;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 主容器点击事件 - 取消输入框焦点
        /// </summary>
        private void MainContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 将焦点移到窗口本身，从而取消输入框的焦点
            FocusManager.SetFocusedElement(this, this);
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// 获取当前 URL 按钮点击
        /// </summary>
        private void BtnGetCurrentUrl_Click(object sender, RoutedEventArgs e)
        {
            // 通过 Owner 链找到 PlayerWindow 获取当前 URL
            var owner = Owner;
            while (owner != null)
            {
                if (owner is PlayerWindow playerWindow)
                {
                    var currentUrl = playerWindow.CurrentUrl;
                    _viewModel.SetCurrentUrl(currentUrl);
                    return;
                }
                owner = owner.Owner;
            }
        }

        /// <summary>
        /// 目录树选择变化
        /// </summary>
        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FolderTreeItem selectedItem)
            {
                _viewModel.OnFolderSelected(selectedItem);
            }
        }

        /// <summary>
        /// 点击目录树容器空白区域时取消选中
        /// </summary>
        private void FolderTreeContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击是否在 TreeViewItem 上
            var hitTestResult = VisualTreeHelper.HitTest(FolderTree, e.GetPosition(FolderTree));
            if (hitTestResult != null)
            {
                // 查找点击位置是否在 TreeViewItem 内
                var element = hitTestResult.VisualHit;
                while (element != null && element != FolderTree)
                {
                    if (element is TreeViewItem)
                    {
                        return; // 点击在 TreeViewItem 上，不处理
                    }
                    element = VisualTreeHelper.GetParent(element) as Visual;
                }
            }

            // 点击在空白区域，清除选中状态
            ClearTreeViewSelection();
        }

        /// <summary>
        /// 清除 TreeView 选中状态
        /// </summary>
        private void ClearTreeViewSelection()
        {
            if (FolderTree.SelectedItem != null)
            {
                // 递归取消所有项的选中状态
                foreach (var item in _viewModel.FolderTreeItems)
                {
                    ClearSelectionRecursive(item);
                }
                _viewModel.ClearFolderSelection();
            }
        }

        /// <summary>
        /// 递归清除选中状态
        /// </summary>
        private void ClearSelectionRecursive(FolderTreeItem item)
        {
            var container = FolderTree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                container.IsSelected = false;
                foreach (var child in item.Children)
                {
                    ClearSelectionInContainer(container, child);
                }
            }
        }

        /// <summary>
        /// 在容器中递归清除选中状态
        /// </summary>
        private void ClearSelectionInContainer(TreeViewItem parentContainer, FolderTreeItem item)
        {
            var container = parentContainer.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                container.IsSelected = false;
                foreach (var child in item.Children)
                {
                    ClearSelectionInContainer(container, child);
                }
            }
        }

        /// <summary>
        /// TreeViewItem 右键点击时先选中该项并显示上下文菜单
        /// </summary>
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeViewItem)
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
                    clickedItem = VisualTreeHelper.GetParent(clickedItem);
                }

                // 选中该项
                treeViewItem.IsSelected = true;
                treeViewItem.Focus();

                // 获取选中的数据项
                if (treeViewItem.DataContext is FolderTreeItem folderItem)
                {
                    // 根目录不显示上下文菜单
                    if (folderItem.IsRoot)
                    {
                        e.Handled = true;
                        return;
                    }

                    // 创建并显示上下文菜单
                    var contextMenu = CreateFolderContextMenu();
                    contextMenu.PlacementTarget = treeViewItem;
                    contextMenu.IsOpen = true;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 创建文件夹上下文菜单
        /// </summary>
        private ContextMenu CreateFolderContextMenu()
        {
            var contextMenu = new ContextMenu { Style = FindResource("DarkContextMenuStyle") as Style };

            var editMenuItem = new MenuItem { Header = "✏️ 编辑", Style = FindResource("DarkMenuItemStyle") as Style };
            editMenuItem.Click += MenuEditFolder_Click;

            var deleteMenuItem = new MenuItem { Header = "🗑️ 删除", Style = FindResource("DarkMenuItemStyle") as Style };
            deleteMenuItem.Click += MenuDeleteFolder_Click;

            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(deleteMenuItem);

            return contextMenu;
        }

        /// <summary>
        /// 编辑文件夹菜单点击
        /// </summary>
        private void MenuEditFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderTree.SelectedItem is FolderTreeItem selectedItem)
            {
                _viewModel.EditFolder(selectedItem);
            }
        }

        /// <summary>
        /// 删除文件夹菜单点击
        /// </summary>
        private void MenuDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderTree.SelectedItem is FolderTreeItem selectedItem)
            {
                _viewModel.DeleteFolder(selectedItem);
            }
        }

        /// <summary>
        /// 新建目录名称输入框按键
        /// </summary>
        private void TxtNewFolderName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.ConfirmNewFolderCommand.CanExecute(null))
                {
                    _viewModel.ConfirmNewFolderCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _viewModel.HideNewFolderCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        /// <summary>
        /// 开荒笔记按钮点击
        /// </summary>
        private void BtnPioneerNotes_Click(object sender, RoutedEventArgs e)
        {
            // 打开开荒笔记窗口（使用工厂方法创建）
            var noteWindow = _pioneerNoteWindowFactory();
            noteWindow.Owner = this.Owner ?? this; // 使用对话框的 Owner 或自己作为 Owner
            noteWindow.ShowDialog();

            // 刷新目录树（可能在开荒笔记中修改了目录）
            _viewModel.LoadFolderTree();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 处理编辑文件夹
        /// </summary>
        private void HandleEditFolder()
        {
            var folderToEdit = _viewModel.FolderToEdit;
            if (folderToEdit == null)
            {
                return;
            }

            // 打开编辑对话框
            var editDialog = _dialogFactory.CreateNoteEditDialog(
                "编辑目录",
                folderToEdit.Name,
                "请输入新的目录名称：");
            editDialog.Owner = this;

            editDialog.ShowDialog();

            if (editDialog.Result == true && !string.IsNullOrWhiteSpace(editDialog.InputText))
            {
                _viewModel.ExecuteEditFolder(editDialog.InputText);
            }
            else
            {
                // 取消编辑，清空状态
                _viewModel.FolderToEdit = null;
            }
        }

        /// <summary>
        /// 处理删除文件夹
        /// </summary>
        private void HandleDeleteFolder()
        {
            var folderToDelete = _viewModel.FolderToDelete;
            if (folderToDelete == null)
            {
                return;
            }

            // 确认删除
            var confirmDialog = new ConfirmDialog(
                $"确定要删除目录 \"{folderToDelete.Name}\" 吗？\n\n该目录下的所有子目录和笔记项也将被删除。",
                "删除目录") { Owner = this };

            confirmDialog.ShowDialog();

            if (confirmDialog.Result == true)
            {
                _viewModel.ExecuteDeleteFolder();
            }
            else
            {
                // 取消删除，清空状态
                _viewModel.FolderToDelete = null;
            }
        }

        /// <summary>
        /// 根据 ID 选中目录
        /// </summary>
        private void SelectFolderById(string folderId)
        {
            // 递归查找并选中目录
            foreach (var item in _viewModel.FolderTreeItems)
            {
                if (SelectFolderInTree(item, folderId))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 在树中递归查找并选中目录
        /// </summary>
        private bool SelectFolderInTree(FolderTreeItem item, string folderId)
        {
            if (item.Id == folderId)
            {
                var container = FolderTree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    container.IsSelected = true;
                    container.BringIntoView();
                    return true;
                }
            }

            foreach (var child in item.Children)
            {
                if (SelectFolderInTree(child, folderId))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
