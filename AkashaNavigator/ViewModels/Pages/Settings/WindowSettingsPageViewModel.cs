using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 窗口设置页面 ViewModel
/// 包含：边缘吸附、吸附阈值、退出时提示记录
/// </summary>
public partial class WindowSettingsPageViewModel : ObservableObject
{
    /// <summary>
    /// 是否启用边缘吸附（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableEdgeSnap;

    /// <summary>
    /// 吸附阈值（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _snapThreshold;

    /// <summary>
    /// 是否在退出时提示记录笔记（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _promptRecordOnExit;

    public WindowSettingsPageViewModel()
    {
        // 默认值，稍后通过 LoadSettings 从 Config 加载
    }

    /// <summary>
    /// 从配置对象加载设置
    /// </summary>
    public void LoadSettings(AppConfig config)
    {
        EnableEdgeSnap = config.EnableEdgeSnap;
        SnapThreshold = config.SnapThreshold;
        PromptRecordOnExit = config.PromptRecordOnExit;
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.EnableEdgeSnap = EnableEdgeSnap;
        config.SnapThreshold = SnapThreshold;
        config.PromptRecordOnExit = PromptRecordOnExit;
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        EnableEdgeSnap = config.EnableEdgeSnap;
        SnapThreshold = config.SnapThreshold;
        PromptRecordOnExit = config.PromptRecordOnExit;
    }
}
