using System.Windows;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;
using FloatWebPlayer.Views;

namespace FloatWebPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        #region Fields

        private PlayerWindow? _playerWindow;
        private ControlBarWindow? _controlBarWindow;
        private HotkeyService? _hotkeyService;
        private OsdWindow? _osdWindow;
        private AppConfig _config = new();

        #endregion

        #region Event Handlers

        /// <summary>
        /// 应用启动事件
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 初始化 ProfileManager（这会自动创建 Default Profile）
            _ = ProfileManager.Instance;
            
            // 初始化 DataService
            _ = DataService.Instance;

            // 创建主窗口（播放器）
            _playerWindow = new PlayerWindow();

            // 创建控制栏窗口
            _controlBarWindow = new ControlBarWindow();

            // 设置窗口间事件关联
            SetupWindowBindings();

            // 显示窗口
            _playerWindow.Show();
            
            // 控制栏窗口启动自动显示/隐藏监听（默认隐藏，鼠标移到顶部触发显示）
            _controlBarWindow.StartAutoShowHide();

            // 启动全局快捷键服务
            StartHotkeyService();
        }

        /// <summary>
        /// 设置两窗口之间的事件绑定
        /// </summary>
        private void SetupWindowBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 控制栏导航请求 → 播放器窗口加载
            _controlBarWindow.NavigateRequested += (s, url) =>
            {
                _playerWindow.Navigate(url);
            };

            // 控制栏后退请求
            _controlBarWindow.BackRequested += (s, e) =>
            {
                _playerWindow.GoBack();
            };

            // 控制栏前进请求
            _controlBarWindow.ForwardRequested += (s, e) =>
            {
                _playerWindow.GoForward();
            };

            // 控制栏刷新请求
            _controlBarWindow.RefreshRequested += (s, e) =>
            {
                _playerWindow.Refresh();
            };

            // 播放器窗口关闭时，关闭控制栏
            _playerWindow.Closed += (s, e) =>
            {
                _controlBarWindow.Close();
            };

            // 播放器 URL 变化时，同步到控制栏
            _playerWindow.UrlChanged += (s, url) =>
            {
                _controlBarWindow.CurrentUrl = url;
            };

            // 播放器导航状态变化时，更新控制栏按钮
            _playerWindow.NavigationStateChanged += (s, e) =>
            {
                _controlBarWindow.UpdateBackButtonState(_playerWindow.CanGoBack);
                _controlBarWindow.UpdateForwardButtonState(_playerWindow.CanGoForward);
            };

            // 收藏按钮点击事件
            _controlBarWindow.BookmarkRequested += (s, e) =>
            {
                var url = _controlBarWindow.CurrentUrl;
                var title = _playerWindow.CurrentTitle;
                var isBookmarked = DataService.Instance.ToggleBookmark(url, title);
                _controlBarWindow.UpdateBookmarkState(isBookmarked);
                ShowOsd(isBookmarked ? "已添加收藏" : "已取消收藏", "⭐");
            };

            // 历史记录菜单事件
            _controlBarWindow.HistoryRequested += (s, e) =>
            {
                var historyWindow = new HistoryWindow();
                historyWindow.HistoryItemSelected += (sender, url) =>
                {
                    _playerWindow.Navigate(url);
                };
                historyWindow.ShowDialog();
            };

            // 收藏夹菜单事件
            _controlBarWindow.BookmarksRequested += (s, e) =>
            {
                var bookmarkPopup = new BookmarkPopup();
                bookmarkPopup.BookmarkItemSelected += (sender, url) =>
                {
                    _playerWindow.Navigate(url);
                };
                bookmarkPopup.ShowDialog();
            };

            // 设置菜单事件
            _controlBarWindow.SettingsRequested += (s, e) =>
            {
                var settingsWindow = new SettingsWindow(_config);
                settingsWindow.SettingsSaved += (sender, config) =>
                {
                    _config = config;
                    // 应用设置变更
                    ApplySettings();
                };
                settingsWindow.ShowDialog();
            };

            // 播放器 URL 变化时，检查收藏状态
            _playerWindow.UrlChanged += (s, url) =>
            {
                var isBookmarked = DataService.Instance.IsBookmarked(url);
                _controlBarWindow.UpdateBookmarkState(isBookmarked);
            };

        }

        /// <summary>
        /// 启动全局快捷键服务
        /// </summary>
        private void StartHotkeyService()
        {
            _hotkeyService = new HotkeyService();

            // 绑定快捷键事件
            _hotkeyService.SeekBackward += (s, e) =>
            {
                _playerWindow?.SeekAsync(-AppConstants.DefaultSeekSeconds);
                ShowOsd($"-{AppConstants.DefaultSeekSeconds}s", "⏪");
            };

            _hotkeyService.SeekForward += (s, e) =>
            {
                _playerWindow?.SeekAsync(AppConstants.DefaultSeekSeconds);
                ShowOsd($"+{AppConstants.DefaultSeekSeconds}s", "⏩");
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
                var isClickThrough = _playerWindow?.ToggleClickThrough();
                if (isClickThrough.HasValue)
                {
                    var msg = isClickThrough.Value ? "鼠标穿透已开启" : "鼠标穿透已关闭";
                    ShowOsd(msg, "👆");
                }
            };

            _hotkeyService.Start();
        }

        /// <summary>
        /// 显示 OSD 提示
        /// </summary>
        /// <param name="message">提示文字</param>
        /// <param name="icon">图标（可选）</param>
        private void ShowOsd(string message, string? icon = null)
        {
            // 延迟初始化 OSD 窗口
            _osdWindow ??= new OsdWindow();
            _osdWindow.ShowMessage(message, icon);
        }

        /// <summary>
        /// 应用设置变更
        /// </summary>
        private void ApplySettings()
        {
            // 设置变更后的逻辑（如透明度、快进秒数等）
            // 可以在这里保存配置到文件
        }

        /// <summary>
        /// 应用退出事件
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            // 先停止快捷键服务
            _hotkeyService?.Dispose();
            
            // 确保控制栏停止定时器
            _controlBarWindow?.StopAutoShowHide();
            
            base.OnExit(e);
        }

        #endregion
    }
}

