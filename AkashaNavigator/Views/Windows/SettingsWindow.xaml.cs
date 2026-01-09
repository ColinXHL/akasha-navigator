using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Windows;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// SettingsWindow - 设置窗口（MVVM 架构）
/// 快捷键逻辑已移至 HotkeyTextBox 自定义控件
/// </summary>
public partial class SettingsWindow : AnimatedWindow
{
#region Fields

    private readonly SettingsViewModel _viewModel;
    private readonly INotificationService _notificationService;
    private bool _isInitializing = true;

#endregion

#region Constructor

    public SettingsWindow(SettingsViewModel viewModel, INotificationService notificationService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        InitializeComponent();
        DataContext = _viewModel;

        // 订阅 ViewModel 事件
        _viewModel.OpenConfigFolderRequested += OnOpenConfigFolder;

        // 加载 Profile 列表
        _viewModel.LoadProfileList();

        _isInitializing = false;
    }

#endregion

#region Private Methods

    /// <summary>
    /// 打开配置文件夹
    /// </summary>
    private void OnOpenConfigFolder(object? sender, string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.TitleBar_MouseLeftButtonDown(sender, e);
    }

    /// <summary>
    /// 快进秒数滑块值变化
    /// </summary>
    private void SeekSecondsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
            return;
        SeekSecondsValue.Text = $"{(int)e.NewValue}s";
    }

    /// <summary>
    /// 透明度滑块值变化
    /// </summary>
    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
            return;
        OpacityValue.Text = $"{(int)e.NewValue}%";
    }

    /// <summary>
    /// 吸附阈值滑块值变化
    /// </summary>
    private void SnapThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
            return;
        SnapThresholdValue.Text = $"{(int)e.NewValue}px";
    }

    /// <summary>
    /// 打开配置文件夹按钮点击
    /// </summary>
    private void BtnOpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenConfigFolderCommand.Execute(null);
    }

    /// <summary>
    /// 取消按钮
    /// </summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    /// <summary>
    /// 保存按钮
    /// </summary>
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        CloseWithAnimation();
    }

    /// <summary>
    /// Profile 选择变化
    /// </summary>
    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
            return;

        // 绑定已处理 Profile 切换，此方法保留用于扩展
    }

#endregion
}
}
