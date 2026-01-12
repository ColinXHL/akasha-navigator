using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 通用设置页面 - Profile 选择、基础设置
/// </summary>
public partial class GeneralSettingsPage : UserControl
{
    /// <summary>
    /// 无参构造函数，DataContext 由父容器设置
    /// </summary>
    public GeneralSettingsPage()
    {
        InitializeComponent();
        // DataContext 将由 SettingsWindow 设置为 SettingsViewModel.GeneralPage
    }
}
}
