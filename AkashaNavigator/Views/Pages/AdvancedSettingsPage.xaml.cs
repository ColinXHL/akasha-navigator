using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 高级设置页面 - 插件更新提示、调试日志
/// </summary>
public partial class AdvancedSettingsPage : UserControl
{
    public AdvancedSettingsPage(AdvancedSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
}
