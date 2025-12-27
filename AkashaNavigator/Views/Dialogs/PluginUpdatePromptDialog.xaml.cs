using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 插件更新提示对话框
/// </summary>
public partial class PluginUpdatePromptDialog : AnimatedWindow
{
#region Properties

    /// <summary>
    /// 有更新的插件列表
    /// </summary>
    public List<UpdateCheckResult> UpdatesAvailable { get; }

    /// <summary>
    /// 用户选择的操作结果
    /// </summary>
    public PluginUpdatePromptResult Result { get; private set; } = PluginUpdatePromptResult.Cancel;

    /// <summary>
    /// 用户是否选择了不再提示
    /// </summary>
    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

#endregion

#region Fields

    private readonly IConfigService _configService;

#endregion

#region Constructor

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public PluginUpdatePromptDialog(IConfigService configService, List<UpdateCheckResult> updates)
    {
        _configService = configService;
        InitializeComponent();
        UpdatesAvailable = updates;

        // 设置提示文字
        var count = updates.Count;
        UpdateMessageText.Text = $"发现 {count} 个插件有可用更新。\n是否立即更新？";
    }

#endregion

#region Event Handlers

    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnOpenPluginCenter_Click(object sender, RoutedEventArgs e)
    {
        Result = PluginUpdatePromptResult.OpenPluginCenter;
        SaveDontShowAgainSetting();
        DialogResult = true;
        Close();
    }

    private void BtnUpdateAll_Click(object sender, RoutedEventArgs e)
    {
        Result = PluginUpdatePromptResult.UpdateAll;
        SaveDontShowAgainSetting();
        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = PluginUpdatePromptResult.Cancel;
        SaveDontShowAgainSetting();
        DialogResult = false;
        Close();
    }

#endregion

#region Private Methods

    private void SaveDontShowAgainSetting()
    {
        if (DontShowAgain)
        {
            var config = _configService.Config;
            config.EnablePluginUpdateNotification = false;
            _configService.Save();
        }
    }

#endregion
}

/// <summary>
/// 插件更新提示对话框的操作结果
/// </summary>
public enum PluginUpdatePromptResult
{
    /// <summary>取消</summary>
    Cancel,
    /// <summary>打开插件中心</summary>
    OpenPluginCenter,
    /// <summary>一键更新</summary>
    UpdateAll
}
}
