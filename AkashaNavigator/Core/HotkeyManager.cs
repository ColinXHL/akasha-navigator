using System;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 全局快捷键管理器
    /// 负责初始化和管理全局快捷键服务
    /// </summary>
    public class HotkeyManager
    {
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
                var seconds = _config.SeekSeconds;
                _playerWindow?.SeekAsync(-seconds);
                ShowOsd($"-{seconds}s", "⏪");
            };

            _hotkeyService.SeekForward += (s, e) =>
            {
                var seconds = _config.SeekSeconds;
                _playerWindow?.SeekAsync(seconds);
                ShowOsd($"+{seconds}s", "⏩");
            };

            _hotkeyService.TogglePlay += (s, e) =>
            {
                _playerWindow?.TogglePlayAsync();
                ShowOsd("播放/暂停", "⏯");
            };

            _hotkeyService.DecreaseOpacity += (s, e) =>
            {
                var opacity = _playerWindow?.DecreaseOpacity();
                if (opacity.HasValue)
                {
                    ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔅");
                }
            };

            _hotkeyService.IncreaseOpacity += (s, e) =>
            {
                var opacity = _playerWindow?.IncreaseOpacity();
                if (opacity.HasValue)
                {
                    ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔆");
                }
            };

            _hotkeyService.ToggleClickThrough += (s, e) =>
            {
                // 最大化时禁用穿透热键
                if (_playerWindow?.IsMaximized == true)
                    return;

                var isClickThrough = _playerWindow?.ToggleClickThrough();
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
