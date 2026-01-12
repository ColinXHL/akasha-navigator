using System;
using System.Windows;
using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 窗口设置页面 - 边缘吸附、吸附阈值、退出提示
/// </summary>
public partial class WindowSettingsPage : UserControl
{
    /// <summary>
    /// 无参构造函数，DataContext 由父容器设置
    /// </summary>
    public WindowSettingsPage()
    {
        InitializeComponent();
        // DataContext 将由 SettingsWindow 设置为 SettingsViewModel.WindowPage

        // 订阅 DataContext 变化，绑定 ViewModel 事件
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 取消旧 ViewModel 的事件订阅
        if (e.OldValue is WindowSettingsPageViewModel oldVm)
        {
            oldVm.ClosePopupRequested -= OnClosePopupRequested;
        }

        // 订阅新 ViewModel 的事件
        if (e.NewValue is WindowSettingsPageViewModel newVm)
        {
            newVm.ClosePopupRequested += OnClosePopupRequested;
        }
    }

    /// <summary>
    /// 选取窗口按钮点击 - 打开 Popup 并刷新进程列表
    /// </summary>
    private void SelectWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowSettingsPageViewModel vm)
        {
            vm.RefreshProcessListCommand.Execute(null);
            ProcessSelectorPopup.IsOpen = true;
        }
    }

    /// <summary>
    /// ViewModel 请求关闭 Popup
    /// </summary>
    private void OnClosePopupRequested(object? sender, EventArgs e)
    {
        ProcessSelectorPopup.IsOpen = false;
    }
}
}
