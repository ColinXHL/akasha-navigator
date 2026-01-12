using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 快捷键设置页面 - 全局控制、视频控制、透明度、窗口行为、播放速率、窗口控制
/// </summary>
public partial class HotkeySettingsPage : UserControl
{
    /// <summary>
    /// 无参构造函数，DataContext 由父容器设置
    /// </summary>
    public HotkeySettingsPage()
    {
        InitializeComponent();
        // DataContext 将由 SettingsWindow 设置为 SettingsViewModel.HotkeysPage
    }
}
}
