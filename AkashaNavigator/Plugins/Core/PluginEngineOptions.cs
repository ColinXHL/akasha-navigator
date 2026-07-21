using System;
using System.Collections.Generic;
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
    private readonly List<Action> _hostObjectCleanupActions = new();
    private bool _hostObjectsCleanedUp;

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

    /// <summary>
    /// 注册由插件引擎创建、需要在 V8 引擎销毁前释放的宿主对象。
    /// </summary>
    internal void RegisterHostObjectCleanup(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);

        if (_hostObjectsCleanedUp)
        {
            throw new ObjectDisposedException(nameof(PluginEngineOptions));
        }

        _hostObjectCleanupActions.Add(cleanup);
    }

    /// <summary>
    /// 以创建顺序的逆序释放宿主对象。该方法是幂等的。
    /// </summary>
    internal void CleanupHostObjects()
    {
        if (_hostObjectsCleanedUp)
        {
            return;
        }

        _hostObjectsCleanedUp = true;

        for (var i = _hostObjectCleanupActions.Count - 1; i >= 0; i--)
        {
            try
            {
                _hostObjectCleanupActions[i]();
            }
            catch (Exception ex)
            {
                (LogService ?? Services.LogService.Instance).Error(
                    nameof(PluginEngineOptions),
                    ex,
                    "清理插件宿主对象失败");
            }
        }

        _hostObjectCleanupActions.Clear();
    }
}
}
