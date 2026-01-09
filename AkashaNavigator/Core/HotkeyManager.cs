using System;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Services;
using Serilog;

namespace AkashaNavigator.Core
{
/// <summary>
/// 全局快捷键管理器
/// 负责初始化和管理全局快捷键服务
/// </summary>
public class HotkeyManager
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "HotkeyManager");

    private HotkeyService? _hotkeyService;
    private PlayerWindow? _playerWindow;
    private AppConfig _config = null!;
    private Action<string, string?>? _showOsdAction;

    /// <summary>
    /// 初始化 HotkeyManager
    /// </summary>
    /// <param name="playerWindow">播放器窗口引用</param>
    /// <param name="config">应用配置</param>
    /// <param name="showOsdAction">显示OSD的回调</param>
    public void Initialize(PlayerWindow playerWindow, AppConfig config, Action<string, string?>? showOsdAction)
    {
        _playerWindow = playerWindow;
        _config = config;
        _showOsdAction = showOsdAction;

        _hotkeyService = new HotkeyService();
        _hotkeyService.UpdateConfig(_config.ToHotkeyConfig());

        SetupHotkeyBindings();
        _hotkeyService.Start();
    }

    /// <summary>
    /// 更新快捷键配置
    /// </summary>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _hotkeyService?.UpdateConfig(_config.ToHotkeyConfig());
    }

    /// <summary>
    /// 设置快捷键事件绑定
    /// </summary>
    private void SetupHotkeyBindings()
    {
        if (_hotkeyService == null)
            return;

        _hotkeyService.SeekBackward += (s, e) =>
        {
            Logger.Debug("SeekBackward event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var seconds = _config.SeekSeconds;
            _playerWindow?.SeekAsync(-seconds);
            ShowOsd($"-{seconds}s", "⏪");
        };

        _hotkeyService.SeekForward += (s, e) =>
        {
            Logger.Debug("SeekForward event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var seconds = _config.SeekSeconds;
            _playerWindow?.SeekAsync(seconds);
            ShowOsd($"+{seconds}s", "⏩");
        };

        _hotkeyService.TogglePlay += (s, e) =>
        {
            Logger.Debug("TogglePlay event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _playerWindow?.TogglePlayAsync();
            ShowOsd("播放/暂停", "⏯");
        };

        _hotkeyService.DecreaseOpacity += (s, e) =>
        {
            Logger.Debug("DecreaseOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var opacity = _playerWindow?.DecreaseOpacity();
            Logger.Debug("DecreaseOpacity returned: {Opacity}", opacity);
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔅");
            }
        };

        _hotkeyService.IncreaseOpacity += (s, e) =>
        {
            Logger.Debug("IncreaseOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var opacity = _playerWindow?.IncreaseOpacity();
            Logger.Debug("IncreaseOpacity returned: {Opacity}", opacity);
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔆");
            }
        };

        _hotkeyService.ToggleClickThrough += (s, e) =>
        {
            Logger.Debug("ToggleClickThrough event received");

            // 最大化时禁用穿透热键
            if (_playerWindow?.IsMaximized == true)
            {
                Logger.Debug("Skipped: window is maximized");
                return;
            }

            Logger.Debug("Calling ToggleClickThrough on PlayerWindow");
            var isClickThrough = _playerWindow?.ToggleClickThrough();
            Logger.Debug("ToggleClickThrough returned: {IsClickThrough}", isClickThrough);

            if (isClickThrough.HasValue)
            {
                var msg = isClickThrough.Value ? "鼠标穿透已开启" : "鼠标穿透已关闭";
                ShowOsd(msg, "👆");
            }
        };

        _hotkeyService.ToggleMaximize += (s, e) =>
        {
            _playerWindow?.ToggleMaximize();
            var msg = _playerWindow?.IsMaximized == true ? "窗口: 最大化" : "窗口: 还原";
            ShowOsd(msg, "🔲");
        };

        _hotkeyService.ResetOpacity += (s, e) =>
        {
            Logger.Debug("ResetOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _playerWindow?.ResetOpacity();
            ShowOsd("透明度: 100%", "🔆");
        };

        _hotkeyService.IncreasePlaybackRate += async (s, e) =>
        {
            Logger.Debug("IncreasePlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.IncreasePlaybackRateAsync();
                ShowOsd($"播放速率: {_playerWindow.CurrentPlaybackRate:F2}x", "⏩");
            }
        };

        _hotkeyService.DecreasePlaybackRate += async (s, e) =>
        {
            Logger.Debug("DecreasePlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.DecreasePlaybackRateAsync();
                ShowOsd($"播放速率: {_playerWindow.CurrentPlaybackRate:F2}x", "⏪");
            }
        };

        _hotkeyService.ResetPlaybackRate += async (s, e) =>
        {
            Logger.Debug("ResetPlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.ResetPlaybackRateAsync();
                ShowOsd("播放速率: 1.0x", "🔄");
            }
        };

        _hotkeyService.ToggleWindowVisibility += (s, e) =>
        {
            Logger.Debug("ToggleWindowVisibility event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _playerWindow?.ToggleVisibility();
            var msg = _playerWindow?.IsHidden == true ? "窗口已隐藏" : "窗口已显示";
            ShowOsd(msg, "👁");
        };

        _hotkeyService.SuspendHotkeys += (s, e) =>
        {
            Logger.Debug("SuspendHotkeys event received");
            if (_hotkeyService != null)
            {
                _hotkeyService.ToggleSuspend();
                var isSuspended = _hotkeyService.IsSuspended;
                var msg = isSuspended ? "热键已暂停" : "热键已恢复";
                ShowOsd(msg, isSuspended ? "⏸" : "▶");
            }
        };
    }

    /// <summary>
    /// 显示 OSD 提示
    /// </summary>
    private void ShowOsd(string message, string? icon = null)
    {
        _showOsdAction?.Invoke(message, icon);
    }

    /// <summary>
    /// 停止并释放快捷键服务
    /// </summary>
    public void Dispose()
    {
        _hotkeyService?.Dispose();
        _hotkeyService = null;
    }
}
}
