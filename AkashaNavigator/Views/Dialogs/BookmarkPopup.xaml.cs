using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.Core.Interfaces;

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

#region Fields

    private readonly BookmarkPopupViewModel _viewModel;
    private readonly IDialogFactory _dialogFactory;

#endregion

#region Constructor

    public BookmarkPopup(BookmarkPopupViewModel viewModel, IDialogFactory dialogFactory)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        InitializeComponent();
        DataContext = _viewModel;

        // 订阅 ViewModel 的选择事件，转换为对外的事件
        _viewModel.ItemSelected += OnViewModelItemSelected;

        // 订阅确认对话框请求事件
        _viewModel.ConfirmDialogRequested += OnConfirmDialogRequested;
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

    /// <summary>
    /// 处理确认对话框请求事件
    /// </summary>
    private void OnConfirmDialogRequested(object? sender, ViewModels.Common.ConfirmDialogRequest request)
    {
        var dialog =
            _dialogFactory.CreateConfirmDialog(request.Message, request.Title, request.ConfirmText, request.CancelText);
        dialog.Owner = this;
        dialog.ShowDialog();

        if (dialog.Result == true)
        {
            request.OnConfirmed?.Invoke();
        }
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 清空全部 - 调用 ViewModel 方法触发确认对话框请求
    /// </summary>
    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearAll();
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
