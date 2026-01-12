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
    }
}
}
