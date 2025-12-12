using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FloatWebPlayer.Helpers;
using Microsoft.Web.WebView2.Core;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// PlayerWindow - 播放器主窗口
    /// </summary>
    public partial class PlayerWindow : Window
    {
        #region Events

        /// <summary>
        /// URL 变化事件
        /// </summary>
        public event EventHandler<string>? UrlChanged;

        /// <summary>
        /// 导航状态变化事件
        /// </summary>
        public event EventHandler? NavigationStateChanged;

        #endregion


        #region Fields

        /// <summary>
        /// 是否最大化
        /// </summary>
        private bool _isMaximized;

        /// <summary>
        /// 最大化前的窗口边界
        /// </summary>
        private Rect _restoreBounds;

        /// <summary>
        /// 是否处于鼠标穿透模式
        /// </summary>
        private bool _isClickThrough;

        /// <summary>
        /// 穿透模式前保存的透明度
        /// </summary>
        private double _opacityBeforeClickThrough = 1.0;

        /// <summary>
        /// 当前窗口透明度（使用 Win32 API 控制）
        /// </summary>
        private double _windowOpacity = 1.0;

        /// <summary>
        /// 穿透模式下的鼠标位置检测定时器
        /// </summary>
        private DispatcherTimer? _clickThroughTimer;

        /// <summary>
        /// 记录穿透模式下鼠标是否在窗口内
        /// </summary>
        private bool _isCursorInWindowWhileClickThrough;

        #endregion

        #region Constructor

        public PlayerWindow()
        {
            InitializeComponent();
            InitializeWindowPosition();
            InitializeWebView();
            
            // 窗口关闭时清理
            Closing += (s, e) =>
            {
                // 停止穿透模式定时器
                StopClickThroughTimer();
                
                // 取消事件订阅
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                    WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    WebView.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
                    WebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                }
            };
            
            // 窗口关闭后退出应用
            Closed += (s, e) =>
            {
                System.Windows.Application.Current.Shutdown();
            };
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化窗口位置和大小
        /// 默认位置：屏幕左下角
        /// 默认大小：屏幕大小的 1/16
        /// </summary>
        private void InitializeWindowPosition()
        {
            // 获取主屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 计算默认大小：屏幕大小的 1/16
            // 1/16 = 1/4 宽度 x 1/4 高度
            Width = Math.Max(workArea.Width / 4, AppConstants.MinWindowWidth);
            Height = Math.Max(workArea.Height / 4, AppConstants.MinWindowHeight);

            // 定位到屏幕左下角
            Left = workArea.Left;
            Top = workArea.Bottom - Height;
        }

        #endregion

        #region WebView2 Initialization

        /// <summary>
        /// 获取 WebView2 UserDataFolder 路径
        /// 用于持久化 Cookie 和其他用户数据
        /// </summary>
        private static string GetUserDataFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FloatWebPlayer",
                "WebView2Data"
            );
        }

        /// <summary>
        /// 初始化 WebView2 控件
        /// 注意：此方法为 async void，是构造函数中调用异步方法的标准模式
        /// 所有异常都在方法内部处理，不会导致未处理异常
        /// </summary>
        private async void InitializeWebView()
        {
            try
            {
                var userDataFolder = GetUserDataFolder();
                
                // 确保目录存在
                Directory.CreateDirectory(userDataFolder);

                // 创建 WebView2 环境，指定 UserDataFolder 以实现 Cookie 持久化
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder
                );

                // 初始化 WebView2
                await WebView.EnsureCoreWebView2Async(env);

                // 注入所有脚本（滚动条样式、控制按钮、拖动区域）
                await ScriptInjector.InjectAllAsync(WebView);
                
                // 监听来自网页的消息
                WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // 监听导航完成事件
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                // 监听 URL 变化（包括 SPA 路由变化）
                WebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

                // 拦截新窗口请求，在当前窗口打开而非弹出新窗口
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // 导航到默认页面
                WebView.CoreWebView2.Navigate(AppConstants.DefaultHomeUrl);
            }
            catch (Exception ex)
            {
                // async void 方法必须在内部处理所有异常
                MessageBox.Show(
                    $"WebView2 初始化失败：{ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 处理来自网页的消息
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            
            switch (message)
            {
                case "minimize":
                    WindowState = WindowState.Minimized;
                    break;
                    
                case "maximize":
                    ToggleMaximize();
                    break;
                    
                case "close":
                    Close();
                    break;
                    
                case "drag":
                    // 使用 Win32 API 启动拖动（不依赖鼠标状态）
                    Dispatcher.BeginInvoke(() =>
                    {
                        Win32Helper.StartMove(this);
                    });
                    break;
            }
        }

        /// <summary>
        /// 导航完成事件处理
        /// </summary>
        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // 触发导航状态变化事件
            NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// URL 变化事件处理
        /// </summary>
        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            // 触发 URL 变化事件
            var currentUrl = WebView.CoreWebView2?.Source ?? string.Empty;
            UrlChanged?.Invoke(this, currentUrl);

            // 触发导航状态变化事件
            NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 拦截新窗口请求，在当前窗口打开
        /// </summary>
        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // 阻止新窗口弹出
            e.Handled = true;
            
            // 在当前 WebView 中导航到目标 URL
            WebView.CoreWebView2.Navigate(e.Uri);
        }

        /// <summary>
        /// 切换最大化/还原
        /// </summary>
        private void ToggleMaximize()
        {
            if (!_isMaximized)
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;
                _isMaximized = true;
            }
            else
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                _isMaximized = false;
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// 是否可以后退
        /// </summary>
        public bool CanGoBack => WebView.CoreWebView2?.CanGoBack ?? false;

        /// <summary>
        /// 是否可以前进
        /// </summary>
        public bool CanGoForward => WebView.CoreWebView2?.CanGoForward ?? false;

        #endregion

        #region Public Methods

        /// <summary>
        /// 导航到指定 URL
        /// </summary>
        public void Navigate(string url)
        {
            if (WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Navigate(url);
            }
        }

        /// <summary>
        /// 后退
        /// </summary>
        public void GoBack()
        {
            if (WebView.CoreWebView2?.CanGoBack == true)
            {
                WebView.CoreWebView2.GoBack();
            }
        }

        /// <summary>
        /// 前进
        /// </summary>
        public void GoForward()
        {
            if (WebView.CoreWebView2?.CanGoForward == true)
            {
                WebView.CoreWebView2.GoForward();
            }
        }

        /// <summary>
        /// 刷新
        /// </summary>
        public void Refresh()
        {
            WebView.CoreWebView2?.Reload();
        }

        /// <summary>
        /// 切换视频播放/暂停
        /// </summary>
        public async void TogglePlayAsync()
        {
            if (WebView.CoreWebView2 == null) return;

            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        if (video.paused) {
                            video.play();
                            return 'playing';
                        } else {
                            video.pause();
                            return 'paused';
                        }
                    }
                    return 'no-video';
                })();
            ";

            try
            {
                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
                // 忽略脚本执行错误
            }
        }

        /// <summary>
        /// 视频快进/倒退
        /// </summary>
        /// <param name="seconds">秒数，正数前进，负数倒退</param>
        public async void SeekAsync(int seconds)
        {
            if (WebView.CoreWebView2 == null) return;

            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.currentTime += {seconds};
                        return video.currentTime;
                    }}
                    return -1;
                }})();
            ";

            try
            {
                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
                // 忽略脚本执行错误
            }
        }

        /// <summary>
        /// 降低透明度
        /// </summary>
        /// <returns>当前透明度</returns>
        public double DecreaseOpacity()
        {
            if (_isClickThrough) return _windowOpacity;

            _windowOpacity = Math.Max(AppConstants.MinOpacity, _windowOpacity - AppConstants.OpacityStep);
            Win32Helper.SetWindowOpacity(this, _windowOpacity);
            return _windowOpacity;
        }

        /// <summary>
        /// 增加透明度
        /// </summary>
        /// <returns>当前透明度</returns>
        public double IncreaseOpacity()
        {
            if (_isClickThrough) return _windowOpacity;

            _windowOpacity = Math.Min(AppConstants.MaxOpacity, _windowOpacity + AppConstants.OpacityStep);
            Win32Helper.SetWindowOpacity(this, _windowOpacity);
            return _windowOpacity;
        }

        /// <summary>
        /// 切换鼠标穿透模式
        /// </summary>
        /// <returns>是否处于穿透模式</returns>
        public bool ToggleClickThrough()
        {
            _isClickThrough = !_isClickThrough;

            if (_isClickThrough)
            {
                // 保存当前透明度
                _opacityBeforeClickThrough = _windowOpacity;
                
                // 启动定时器检测鼠标位置
                StartClickThroughTimer();
            }
            else
            {
                // 停止定时器
                StopClickThroughTimer();
                
                // 恢复之前的透明度
                _windowOpacity = _opacityBeforeClickThrough;
                Win32Helper.SetWindowOpacity(this, _windowOpacity);
            }

            Win32Helper.SetClickThrough(this, _isClickThrough);
            return _isClickThrough;
        }

        /// <summary>
        /// 启动穿透模式鼠标检测定时器
        /// </summary>
        private void StartClickThroughTimer()
        {
            _clickThroughTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _clickThroughTimer.Tick += ClickThroughTimer_Tick;
            _clickThroughTimer.Start();
            
            // 立即检测一次
            UpdateClickThroughOpacity();
        }

        /// <summary>
        /// 停止穿透模式鼠标检测定时器
        /// </summary>
        private void StopClickThroughTimer()
        {
            if (_clickThroughTimer != null)
            {
                _clickThroughTimer.Stop();
                _clickThroughTimer.Tick -= ClickThroughTimer_Tick;
                _clickThroughTimer = null;
            }
            _isCursorInWindowWhileClickThrough = false;
        }

        /// <summary>
        /// 定时器回调：检测鼠标位置并更新透明度
        /// </summary>
        private void ClickThroughTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClickThroughOpacity();
        }

        /// <summary>
        /// 更新穿透模式下的透明度
        /// </summary>
        private void UpdateClickThroughOpacity()
        {
            bool cursorInWindow = Win32Helper.IsCursorInWindow(this);
            
            // 只有状态变化时才更新透明度
            if (cursorInWindow != _isCursorInWindowWhileClickThrough)
            {
                _isCursorInWindowWhileClickThrough = cursorInWindow;
                
                if (cursorInWindow)
                {
                    // 鼠标进入窗口，降至最低透明度
                    Win32Helper.SetWindowOpacity(this, AppConstants.MinOpacity);
                }
                else
                {
                    // 鼠标离开窗口，恢复正常透明度
                    Win32Helper.SetWindowOpacity(this, _opacityBeforeClickThrough);
                }
            }
        }

        /// <summary>
        /// 获取当前透明度百分比
        /// </summary>
        public int OpacityPercent => (int)(_windowOpacity * 100);

        /// <summary>
        /// 是否处于鼠标穿透模式
        /// </summary>
        public bool IsClickThrough => _isClickThrough;

        #endregion

        #region Event Handlers

        /// <summary>
        /// 窗口源初始化完成
        /// </summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 预留：后续可添加其他初始化逻辑
        }

        /// <summary>
        /// 鼠标左键按下：边框区域调整大小，其他区域拖动窗口
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            var position = e.GetPosition(this);
            var direction = Win32Helper.GetResizeDirection(this, position, AppConstants.ResizeBorderThickness);

            if (direction != Win32Helper.ResizeDirection.None)
            {
                // 在边框区域，开始调整大小
                Win32Helper.StartResize(this, direction);
            }
            else
            {
                // 非边框区域，拖动窗口
                DragMove();
            }
        }

        /// <summary>
        /// 鼠标移动：更新光标样式
        /// </summary>
        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var position = e.GetPosition(this);
            var direction = Win32Helper.GetResizeDirection(this, position, AppConstants.ResizeBorderThickness);

            Cursor = direction switch
            {
                Win32Helper.ResizeDirection.Left => Cursors.SizeWE,
                Win32Helper.ResizeDirection.Right => Cursors.SizeWE,
                Win32Helper.ResizeDirection.Top => Cursors.SizeNS,
                Win32Helper.ResizeDirection.Bottom => Cursors.SizeNS,
                Win32Helper.ResizeDirection.TopLeft => Cursors.SizeNWSE,
                Win32Helper.ResizeDirection.BottomRight => Cursors.SizeNWSE,
                Win32Helper.ResizeDirection.TopRight => Cursors.SizeNESW,
                Win32Helper.ResizeDirection.BottomLeft => Cursors.SizeNESW,
                _ => Cursors.Arrow
            };
        }

        #endregion
    }
}
