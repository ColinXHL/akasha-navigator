using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 通用设置页面 - Profile 选择、基础设置
/// </summary>
public partial class GeneralSettingsPage : UserControl
{
    public GeneralSettingsPage(GeneralSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
}
