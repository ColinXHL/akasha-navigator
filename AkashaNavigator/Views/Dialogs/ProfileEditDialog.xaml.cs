using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// Profile 编辑对话框
/// </summary>
public partial class ProfileEditDialog : AnimatedWindow
{
#region Properties

    /// <summary>
    /// 是否确认保存
    /// </summary>
    public bool IsConfirmed { get; private set; }

#endregion

#region Fields

    private readonly ProfileEditDialogViewModel _viewModel;

#endregion

#region Constructor

    /// <summary>
    /// 创建 Profile 编辑对话框
    /// </summary>
    /// <param name="viewModel">ViewModel</param>
    public ProfileEditDialog(ProfileEditDialogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        InitializeComponent();

        DataContext = _viewModel;

        // 初始化图标选择器（UI 逻辑保留在 Code-behind）
        InitializeIconSelector();

        // 订阅 ViewModel 的关闭请求
        _viewModel.RequestClose += OnRequestClose;
        _viewModel.RequestClosePopup += OnRequestClosePopup;
    }

#endregion

#region Icon Selector(UI 逻辑)

    /// <summary>
    /// 初始化图标选择器（UI 逻辑保留在 Code-behind）
    /// </summary>
    private void InitializeIconSelector()
    {
        var originalIcon = _viewModel.SelectedIcon;

        foreach (var icon in _viewModel.AvailableIcons)
        {
            var radioButton = new RadioButton { Content = icon, FontSize = 16, GroupName = "IconGroup", Tag = icon,
                                                IsChecked = icon == originalIcon };
            radioButton.Style = (Style)FindResource("IconButtonStyle");
            radioButton.Checked += IconButton_Checked;

            IconPanel.Children.Add(radioButton);
        }
    }

    private void IconButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string icon)
        {
            _viewModel.SelectedIcon = icon;
        }
    }

#endregion

#region UI Event Handlers

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 关闭按钮
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseCommand.Execute(null);
    }

    /// <summary>
    /// 处理 ViewModel 的关闭请求
    /// </summary>
    private void OnRequestClose(object? sender, bool? dialogResult)
    {
        IsConfirmed = dialogResult == true;
        DialogResult = dialogResult;
        CloseWithAnimation();
    }

    /// <summary>
    /// 选取窗口按钮点击
    /// </summary>
    private void SelectWindowButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshProcessListCommand.Execute(null);
        ProcessSelectorPopup.IsOpen = true;
    }

    /// <summary>
    /// 关闭进程选择 Popup
    /// </summary>
    private void OnRequestClosePopup(object? sender, System.EventArgs e)
    {
        ProcessSelectorPopup.IsOpen = false;
    }

#endregion
}
}
