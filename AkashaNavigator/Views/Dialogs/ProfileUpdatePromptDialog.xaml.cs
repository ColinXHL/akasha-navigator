using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// Profile 更新提示对话框。
/// </summary>
public partial class ProfileUpdatePromptDialog : AnimatedWindow
{
    private readonly ProfileUpdatePromptDialogViewModel _viewModel;

    public ProfileUpdatePromptResult Result => _viewModel.Result;

    public ProfileUpdatePromptDialog(ProfileUpdatePromptDialogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
    }

    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnRequestClose(object? sender, ProfileUpdatePromptResult result)
    {
        DialogResult = result != ProfileUpdatePromptResult.Cancel;
        Close();
    }
}
}
