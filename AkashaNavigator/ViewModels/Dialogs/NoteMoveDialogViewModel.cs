using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Models.PioneerNote;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 笔记移动对话框 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class NoteMoveDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 目录列表
        /// </summary>
        public ObservableCollection<FolderItem> Folders { get; } = new();

        /// <summary>
        /// 选中的目录项（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private FolderItem? _selectedFolder;

        /// <summary>
        /// 对话框结果
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 选中的目录 ID（null 表示根目录）
        /// </summary>
        public string? SelectedFolderId => SelectedFolder?.Id;

        /// <summary>
        /// 请求关闭事件
        /// </summary>
        public event EventHandler<bool?>? RequestClose;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="folders">所有目录列表</param>
        /// <param name="currentFolderId">当前所在目录 ID</param>
        public NoteMoveDialogViewModel(List<NoteFolder> folders, string? currentFolderId)
        {
            LoadFolders(folders, currentFolderId);
        }

        /// <summary>
        /// 加载目录列表
        /// </summary>
        private void LoadFolders(List<NoteFolder> folders, string? currentFolderId)
        {
            // 构建目录列表（包含根目录选项）
            var folderItems = new List<FolderItem>
            {
                new FolderItem { Id = null, Name = "根目录", Icon = "🏠", Indent = 0 }
            };

            // 添加所有目录（扁平化显示，带缩进）
            AddFoldersRecursive(folderItems, folders, null, 0);

            Folders.Clear();
            foreach (var item in folderItems)
            {
                Folders.Add(item);
            }

            // 选中当前目录
            var currentItem = folderItems.FirstOrDefault(f => f.Id == currentFolderId);
            if (currentItem != null)
            {
                SelectedFolder = currentItem;
            }
            else
            {
                // 默认选中根目录
                SelectedFolder = Folders.FirstOrDefault();
            }
        }

        /// <summary>
        /// 递归添加目录到列表
        /// </summary>
        private void AddFoldersRecursive(List<FolderItem> items, List<NoteFolder> allFolders, string? parentId, int indent)
        {
            var childFolders = allFolders.Where(f => f.ParentId == parentId).OrderBy(f => f.SortOrder).ToList();

            foreach (var folder in childFolders)
            {
                var prefix = new string(' ', indent * 4);
                items.Add(new FolderItem
                {
                    Id = folder.Id,
                    Name = prefix + folder.Name,
                    Icon = folder.Icon ?? "📁",
                    Indent = indent
                });

                // 递归添加子目录
                AddFoldersRecursive(items, allFolders, folder.Id, indent + 1);
            }
        }

        /// <summary>
        /// 确认移动（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            DialogResult = true;
            RequestClose?.Invoke(this, true);
        }

        /// <summary>
        /// 是否可以确认（有选中项）
        /// </summary>
        private bool CanConfirm() => SelectedFolder != null;

        /// <summary>
        /// 取消（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke(this, false);
        }

        /// <summary>
        /// 关闭窗口（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            RequestClose?.Invoke(this, false);
        }
    }
}
