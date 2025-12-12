using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 控制栏显示状态
    /// </summary>
    public enum ControlBarDisplayState
    {
        /// <summary>完全隐藏</summary>
        Hidden,
        /// <summary>显示触发细线</summary>
        TriggerLine,
        /// <summary>完全展开</summary>
        Expanded
    }

    /// <summary>
    /// ControlBarWindow - URL 控制栏窗口
    /// </summary>
    public partial class ControlBarWindow : Window
    {
        #region Win32 API

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Events

        /// <summary>
        /// 导航请求事件
        /// </summary>
        public event EventHandler<string>? NavigateRequested;

        /// <summary>
        /// 后退请求事件
        /// </summary>
        public event EventHandler? BackRequested;

        /// <summary>
        /// 前进请求事件
        /// </summary>
        public event EventHandler? ForwardRequested;

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// 收藏请求事件
        /// </summary>
        public event EventHandler? BookmarkRequested;

        /// <summary>
        /// 菜单请求事件
        /// </summary>
        public event EventHandler? MenuRequested;

        #endregion

        #region Fields

        /// <summary>
        /// 是否正在拖动
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// 拖动起始点的 X 坐标（屏幕坐标）
        /// </summary>
        private double _dragStartX;

        /// <summary>
        /// 拖动起始时窗口的 Left 值
        /// </summary>
        private double _windowStartLeft;

        /// <summary>
        /// 当前显示状态
        /// </summary>
        private ControlBarDisplayState _displayState = ControlBarDisplayState.Hidden;

        /// <summary>
        /// 鼠标位置检测定时器
        /// </summary>
        private DispatcherTimer? _mouseCheckTimer;

        /// <summary>
        /// 延迟隐藏定时器
        /// </summary>
        private DispatcherTimer? _hideDelayTimer;

        /// <summary>
        /// 展开时的高度
        /// </summary>
        private const double ExpandedHeight = 50;

        /// <summary>
        /// 触发细线状态的窗口高度（比触发线视觉高度大，方便悬停触发）
        /// </summary>
        private const double TriggerLineHeight = 16;

        /// <summary>
        /// 屏幕顶部触发区域比例
        /// </summary>
        private const double TriggerAreaRatio = 1.0 / 4.0;

        /// <summary>
        /// 延迟隐藏时间（毫秒）
        /// </summary>
        private const int HideDelayMs = 400;

        /// <summary>
        /// 状态切换后的稳定期（防抖）
        /// </summary>
        private DateTime _lastStateChangeTime = DateTime.MinValue;
        private const int StateStabilityMs = 150;

        /// <summary>
        /// 窗口内容边距（与 XAML 中 MainBorder 的 Margin 一致）
        /// </summary>
        private const double ContentMargin = 4;

        #endregion

        #region Constructor

        public ControlBarWindow()
        {
            InitializeComponent();
            InitializeWindowPosition();
            InitializeAutoShowHide();
        }

        #endregion

        #region Properties

        /// <summary>
        /// 获取或设置当前 URL
        /// </summary>
        public string CurrentUrl
        {
            get => UrlTextBox.Text;
            set => UrlTextBox.Text = value;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化窗口位置和大小
        /// 位置：屏幕顶部，水平居中
        /// 宽度：屏幕宽度的 1/3
        /// </summary>
        private void InitializeWindowPosition()
        {
            // 获取主屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 计算宽度：屏幕宽度的 1/3，最小 400px
            Width = Math.Max(workArea.Width / 3, 400);

            // 水平居中
            Left = workArea.Left + (workArea.Width - Width) / 2;

            // 顶部定位（留 2px 边距）
            Top = workArea.Top + 2;
        }

        /// <summary>
        /// 初始化自动显示/隐藏功能
        /// </summary>
        private void InitializeAutoShowHide()
        {
            // 初始化鼠标位置检测定时器
            _mouseCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _mouseCheckTimer.Tick += MouseCheckTimer_Tick;

            // 初始化延迟隐藏定时器
            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HideDelayMs)
            };
            _hideDelayTimer.Tick += HideDelayTimer_Tick;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 窗口源初始化完成
        /// </summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 设置 WS_EX_TOOLWINDOW 样式，从 Alt+Tab 中隐藏窗口
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// 拖动条鼠标按下：开始拖动
        /// </summary>
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _isDragging = true;
                _dragStartX = PointToScreen(e.GetPosition(this)).X;
                _windowStartLeft = Left;

                // 捕获鼠标
                Mouse.Capture(DragBar);

                // 注册鼠标移动和释放事件
                DragBar.MouseMove += DragBar_MouseMove;
                DragBar.MouseLeftButtonUp += DragBar_MouseLeftButtonUp;
            }
        }

        /// <summary>
        /// 拖动条鼠标移动：执行水平拖动
        /// </summary>
        private void DragBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentX = PointToScreen(e.GetPosition(this)).X;
                var deltaX = currentX - _dragStartX;

                // 计算新位置
                var newLeft = _windowStartLeft + deltaX;

                // 限制在屏幕范围内
                var workArea = SystemParameters.WorkArea;
                newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - Width));

                Left = newLeft;
            }
        }

        /// <summary>
        /// 拖动条鼠标释放：结束拖动
        /// </summary>
        private void DragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;

                // 释放鼠标捕获
                Mouse.Capture(null);

                // 取消事件注册
                DragBar.MouseMove -= DragBar_MouseMove;
                DragBar.MouseLeftButtonUp -= DragBar_MouseLeftButtonUp;
            }
        }

        /// <summary>
        /// URL 地址栏按键事件
        /// </summary>
        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToCurrentUrl();
            }
        }

        /// <summary>
        /// 前往按钮点击
        /// </summary>
        private void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCurrentUrl();
        }

        /// <summary>
        /// 导航到当前 URL 地址栏中的地址
        /// </summary>
        private void NavigateToCurrentUrl()
        {
            var url = UrlTextBox.Text.Trim();
            
            if (!string.IsNullOrEmpty(url))
            {
                // 自动补全 URL scheme
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                NavigateRequested?.Invoke(this, url);
            }

            // 移除焦点
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// 后退按钮点击
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 前进按钮点击
        /// </summary>
        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            ForwardRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 收藏按钮点击
        /// </summary>
        private void BtnBookmark_Click(object sender, RoutedEventArgs e)
        {
            BookmarkRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 菜单按钮点击
        /// </summary>
        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Auto Show/Hide Logic

        /// <summary>
        /// 鼠标位置检测定时器回调
        /// </summary>
        private void MouseCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (!GetCursorPos(out POINT cursorPos))
                return;

            // 防抖：状态切换后短暂稳定期内不做处理
            if ((DateTime.Now - _lastStateChangeTime).TotalMilliseconds < StateStabilityMs)
                return;

            var workArea = SystemParameters.WorkArea;
            var triggerAreaHeight = workArea.Height * TriggerAreaRatio;

            // 检查鼠标是否在窗口范围内（水平方向，考虑边距）
            bool isInWindowHorizontalRange = cursorPos.X >= Left + ContentMargin && 
                                             cursorPos.X <= Left + Width - ContentMargin;

            // 检查鼠标是否在窗口内（考虑边距）
            bool isMouseOverWindow = isInWindowHorizontalRange &&
                                     cursorPos.Y >= Top && 
                                     cursorPos.Y <= Top + Height - ContentMargin;

            // 检查鼠标是否在屏幕顶部触发区域（整个屏幕宽度）
            bool isInTriggerArea = cursorPos.Y >= workArea.Top &&
                                   cursorPos.Y <= workArea.Top + triggerAreaHeight;

            // 根据当前状态和鼠标位置决定目标状态
            ControlBarDisplayState targetState = _displayState;

            switch (_displayState)
            {
                case ControlBarDisplayState.Hidden:
                    if (isInTriggerArea)
                    {
                        targetState = ControlBarDisplayState.TriggerLine;
                    }
                    break;

                case ControlBarDisplayState.TriggerLine:
                    if (isMouseOverWindow)
                    {
                        targetState = ControlBarDisplayState.Expanded;
                        StopHideDelayTimer();
                    }
                    else if (!isInTriggerArea)
                    {
                        StartHideDelayTimer();
                    }
                    else
                    {
                        StopHideDelayTimer();
                    }
                    break;

                case ControlBarDisplayState.Expanded:
                    if (isMouseOverWindow)
                    {
                        StopHideDelayTimer();
                    }
                    else
                    {
                        // 不在窗口上，启动延迟隐藏
                        StartHideDelayTimer();
                    }
                    break;
            }

            // 应用状态变化
            if (targetState != _displayState)
            {
                SetDisplayState(targetState);
            }
        }

        /// <summary>
        /// 延迟隐藏定时器回调
        /// </summary>
        private void HideDelayTimer_Tick(object? sender, EventArgs e)
        {
            _hideDelayTimer?.Stop();

            // 再次检查鼠标位置，确保真的要隐藏
            if (!GetCursorPos(out POINT cursorPos))
            {
                SetDisplayState(ControlBarDisplayState.Hidden);
                return;
            }

            // 检查鼠标是否在窗口内（考虑边距）
            bool isInWindowHorizontalRange = cursorPos.X >= Left + ContentMargin && 
                                             cursorPos.X <= Left + Width - ContentMargin;
            bool isMouseOverWindow = isInWindowHorizontalRange &&
                                     cursorPos.Y >= Top && 
                                     cursorPos.Y <= Top + Height - ContentMargin;

            // 只要不在窗口上就隐藏（不考虑触发区域）
            if (!isMouseOverWindow)
            {
                SetDisplayState(ControlBarDisplayState.Hidden);
            }
        }

        /// <summary>
        /// 启动延迟隐藏定时器
        /// </summary>
        private void StartHideDelayTimer()
        {
            if (_hideDelayTimer != null && !_hideDelayTimer.IsEnabled)
            {
                _hideDelayTimer.Start();
            }
        }

        /// <summary>
        /// 停止延迟隐藏定时器
        /// </summary>
        private void StopHideDelayTimer()
        {
            _hideDelayTimer?.Stop();
        }

        /// <summary>
        /// 设置显示状态
        /// </summary>
        private void SetDisplayState(ControlBarDisplayState state)
        {
            if (_displayState == state)
                return;

            _displayState = state;
            _lastStateChangeTime = DateTime.Now;

            switch (state)
            {
                case ControlBarDisplayState.Hidden:
                    // 重置为 TriggerLine 状态的视觉效果，避免下次显示时闪烁
                    MainBorder.Opacity = 0;
                    MainBorder.Visibility = Visibility.Collapsed;
                    TriggerLineBorder.Visibility = Visibility.Collapsed;
                    Height = TriggerLineHeight;
                    Hide();
                    break;

                case ControlBarDisplayState.TriggerLine:
                    // 先确保主容器不可见（使用 Opacity 立即生效）
                    MainBorder.Opacity = 0;
                    MainBorder.Visibility = Visibility.Collapsed;
                    TriggerLineBorder.Visibility = Visibility.Collapsed;
                    // 设置高度
                    Height = TriggerLineHeight;
                    // 显示触发线
                    TriggerLineBorder.Visibility = Visibility.Visible;
                    if (!IsVisible)
                    {
                        Show();
                    }
                    break;

                case ControlBarDisplayState.Expanded:
                    // 先隐藏触发线
                    TriggerLineBorder.Visibility = Visibility.Collapsed;
                    // 设置高度
                    Height = ExpandedHeight;
                    // 显示主容器（先设置 Opacity 为 0，再设置 Visibility，最后恢复 Opacity）
                    MainBorder.Opacity = 0;
                    MainBorder.Visibility = Visibility.Visible;
                    MainBorder.Opacity = 1;
                    if (!IsVisible)
                    {
                        Show();
                    }
                    break;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 更新后退按钮状态
        /// </summary>
        public void UpdateBackButtonState(bool canGoBack)
        {
            BtnBack.IsEnabled = canGoBack;
        }

        /// <summary>
        /// 更新前进按钮状态
        /// </summary>
        public void UpdateForwardButtonState(bool canGoForward)
        {
            BtnForward.IsEnabled = canGoForward;
        }

        /// <summary>
        /// 更新收藏按钮状态（是否已收藏）
        /// </summary>
        public void UpdateBookmarkState(bool isBookmarked)
        {
            // 更新收藏按钮图标
            var textBlock = BtnBookmark.Content as System.Windows.Controls.TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = isBookmarked ? "★" : "☆";
            }
        }

        #endregion

        #region Public Control Methods

        /// <summary>
        /// 启动自动显示/隐藏监听
        /// </summary>
        public void StartAutoShowHide()
        {
            SetDisplayState(ControlBarDisplayState.Hidden);
            _mouseCheckTimer?.Start();
        }

        /// <summary>
        /// 停止自动显示/隐藏监听
        /// </summary>
        public void StopAutoShowHide()
        {
            _mouseCheckTimer?.Stop();
            _hideDelayTimer?.Stop();
        }

        #endregion
    }
}
