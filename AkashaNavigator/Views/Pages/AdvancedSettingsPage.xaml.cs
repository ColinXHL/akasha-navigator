using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 高级设置页面 - 插件更新提示、调试日志
/// </summary>
public partial class AdvancedSettingsPage : UserControl
{
    /// <summary>
    /// 无参构造函数，DataContext 由父容器设置
    /// </summary>
    public AdvancedSettingsPage()
    {
        InitializeComponent();
        // DataContext 将由 SettingsWindow 设置为 SettingsViewModel.AdvancedPage
    }
}
}
