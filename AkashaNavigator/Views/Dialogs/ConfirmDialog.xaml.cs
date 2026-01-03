using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// ConfirmDialog - 确认对话框
/// 继承 AnimatedWindow，提供淡入淡出动画效果
/// 用于替代系统 MessageBox 的确认对话框
/// 采用 MVVM 模式，ViewModel 处理业务逻辑
/// </summary>
public partial class ConfirmDialog : AnimatedWindow
{
#region Fields

private readonly ConfirmDialogViewModel _viewModel;

#endregion

#region Properties

/// <summary>
/// 对话框结果：true=确定，false=取消，null=关闭按钮
/// </summary>
public bool? Result => _viewModel.Result;

#endregion

#region Constructor

/// <summary>
/// 创建确认对话框（带 ViewModel）
/// </summary>
/// <param name="viewModel">ViewModel 实例</param>
public ConfirmDialog(ConfirmDialogViewModel viewModel)
{
    _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    InitializeComponent();
    DataContext = _viewModel;

    // 订阅 ViewModel 的关闭请求事件
    _viewModel.CloseRequested += OnCloseRequested;
}

#endregion

#region Event Handlers

/// <summary>
/// 处理 ViewModel 的关闭请求
/// </summary>
private void OnCloseRequested(object? sender, EventArgs e)
{
    CloseWithAnimation();
}

/// <summary>
/// 标题栏拖动（保留 UI 逻辑）
/// </summary>
private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    base.TitleBar_MouseLeftButtonDown(sender, e);
}

#endregion
}
}
