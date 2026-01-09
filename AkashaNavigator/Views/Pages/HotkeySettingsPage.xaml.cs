using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 快捷键设置页面 - 全局控制、视频控制、透明度、窗口行为、播放速率、窗口控制
/// </summary>
public partial class HotkeySettingsPage : UserControl
{
    public HotkeySettingsPage(HotkeySettingsPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
}
