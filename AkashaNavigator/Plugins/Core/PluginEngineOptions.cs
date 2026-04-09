using System;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins.Core
{
/// <summary>
/// 插件引擎初始化选项
/// </summary>
public class PluginEngineOptions
{
    /// <summary>
    /// 当前 Profile ID
    /// </summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// 当前 Profile 名称
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// 当前 Profile 目录
    /// </summary>
    public string? ProfileDirectory { get; set; }

    /// <summary>
    /// 运行时 PlayerWindow 桥接
    /// </summary>
    public IPlayerRuntimeBridge? RuntimeBridge { get; set; }

    public IOverlayManager? OverlayManager { get; set; }

    public IPanelManager? PanelManager { get; set; }

    public ICursorDetectionService? CursorDetectionService { get; set; }

    public ISubtitleService? SubtitleService { get; set; }

    public ScriptExecutionQueue? ScriptExecutionQueue { get; set; }

    public HotkeyService? HotkeyService { get; set; }

    public ActionDispatcher? ActionDispatcher { get; set; }

    public ILogService? LogService { get; set; }

    /// <summary>
    /// OSD 管理器（用于显示屏幕提示）
    /// </summary>
    public AkashaNavigator.Core.OsdManager? OsdManager { get; set; }
}
}
