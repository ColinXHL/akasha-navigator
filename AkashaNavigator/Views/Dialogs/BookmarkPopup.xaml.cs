using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// BookmarkPopup - 收藏夹弹出窗口
    /// </summary>
    public partial class BookmarkPopup : AnimatedWindow
    {
        #region Events

        /// <summary>
        /// 选择收藏项事件
        /// </summary>
        public event EventHandler<string>? BookmarkItemSelected;

        #endregion

        #region Constructor

        private readonly BookmarkPopupViewModel _viewModel;

        public BookmarkPopup(BookmarkPopupViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // 订阅 ViewModel 的选择事件，转换为对外的事件
            _viewModel.ItemSelected += OnViewModelItemSelected;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 处理 ViewModel 的选择事件
        /// </summary>
        private void OnViewModelItemSelected(object? sender, BookmarkItem? item)
        {
            if (item != null)
            {
                CloseWithAnimation(() => BookmarkItemSelected?.Invoke(this, item.Url));
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 清空全部
        /// </summary>
        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateConfirmDialog("确定要清空所有收藏吗？此操作不可撤销。", "确认清空", "清空", "取消");
            dialog.Owner = this;
            dialog.ShowDialog();

            if (dialog.Result == true)
            {
                _viewModel.ClearAll();
            }
        }

        /// <summary>
        /// 双击打开链接
        /// </summary>
        private void BookmarkList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarkList.SelectedItem is BookmarkItem item)
            {
                // 调用 ViewModel 的选择方法
                _viewModel.SelectItemCommand.Execute(item);
            }
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        #endregion
    }
}
