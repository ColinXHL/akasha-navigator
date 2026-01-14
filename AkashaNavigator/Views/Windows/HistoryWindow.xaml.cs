using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Data;
using AkashaNavigator.ViewModels.Common;
using AkashaNavigator.ViewModels.Windows;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// HistoryWindow - 历史记录窗口
/// </summary>
public partial class HistoryWindow : AnimatedWindow
{
#region Events

    /// <summary>
    /// 选择历史记录项事件
    /// </summary>
    public event EventHandler<string>? HistoryItemSelected;

#endregion

#region Constructor

    private readonly HistoryWindowViewModel _viewModel;
    private readonly IDialogFactory _dialogFactory;

    public HistoryWindow(HistoryWindowViewModel viewModel, IDialogFactory dialogFactory)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        InitializeComponent();
        DataContext = _viewModel;

        // 订阅 ViewModel 的选择事件
        _viewModel.ItemSelected += OnViewModelItemSelected;

        // 订阅 ViewModel 的确认对话框请求事件
        _viewModel.ConfirmDialogRequested += OnConfirmDialogRequested;
    }

#endregion

#region Event Handlers

    /// <summary>
    /// ViewModel 选择事件处理
    /// </summary>
    private void OnViewModelItemSelected(object? sender, HistoryItem? item)
    {
        if (item != null)
        {
            CloseWithAnimation(() => HistoryItemSelected?.Invoke(this, item.Url));
        }
    }

    /// <summary>
    /// 确认对话框请求事件处理
    /// </summary>
    private void OnConfirmDialogRequested(object? sender, ConfirmDialogRequest request)
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

    /// <summary>
    /// 搜索框文本变化（已通过绑定处理，此方法保留用于调试或扩展）
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 绑定已处理，此方法可删除或保留用于调试
    }

    /// <summary>
    /// 清空全部按钮点击事件
    /// 调用 ViewModel 命令，由 ViewModel 触发确认对话框请求事件
    /// </summary>
    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearAllCommand.Execute(null);
    }

    /// <summary>
    /// 双击打开链接
    /// </summary>
    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
        {
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
