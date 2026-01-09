using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 高级设置页面 ViewModel
/// 包含：插件更新提示、调试日志
/// </summary>
public partial class AdvancedSettingsPageViewModel : ObservableObject
{
    /// <summary>
    /// 是否启用插件更新通知（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enablePluginUpdateNotification;

    /// <summary>
    /// 是否启用调试日志（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableDebugLog;

    public AdvancedSettingsPageViewModel()
    {
        // 默认值，稍后通过 LoadSettings 从 Config 加载
    }

    /// <summary>
    /// 从配置对象加载设置
    /// </summary>
    public void LoadSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnableDebugLog = config.EnableDebugLog;
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.EnablePluginUpdateNotification = EnablePluginUpdateNotification;
        config.EnableDebugLog = EnableDebugLog;
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnableDebugLog = config.EnableDebugLog;
    }
}
