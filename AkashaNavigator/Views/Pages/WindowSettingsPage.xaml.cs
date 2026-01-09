using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 窗口设置页面 - 边缘吸附、吸附阈值、退出提示
/// </summary>
public partial class WindowSettingsPage : UserControl
{
    public WindowSettingsPage(WindowSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
}
