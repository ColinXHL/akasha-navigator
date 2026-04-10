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
    /// Legacy fallback only. Prefer HostObjectFactory.
    /// </summary>
    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public IPlayerRuntimeBridge? RuntimeBridge { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public IOverlayManager? OverlayManager { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public IPanelManager? PanelManager { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public ICursorDetectionService? CursorDetectionService { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public ISubtitleService? SubtitleService { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public ScriptExecutionQueue? ScriptExecutionQueue { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public HotkeyService? HotkeyService { get; set; }

    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public ActionDispatcher? ActionDispatcher { get; set; }

    public ILogService? LogService { get; set; }

    /// <summary>
    /// Preferred API construction path.
    /// </summary>
    public IPluginHostObjectFactory? HostObjectFactory { get; set; }

    /// <summary>
    /// Legacy fallback only. Prefer HostObjectFactory.
    /// </summary>
    [Obsolete("Legacy fallback mode only. Prefer HostObjectFactory.", false)]
    public AkashaNavigator.Core.OsdManager? OsdManager { get; set; }
}
}
