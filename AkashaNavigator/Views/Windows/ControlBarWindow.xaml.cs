using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Windows;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AkashaNavigator.Views.Windows
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
/// 采用混合架构：Code-Behind 处理 UI 逻辑，ViewModel 处理业务逻辑
/// </summary>
public partial class ControlBarWindow : Window
{

#region Fields

    private readonly ControlBarViewModel _viewModel;
    private readonly PlayerWindow _playerWindow;

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
    /// 状态切换后的稳定期（防抖）
    /// </summary>
    private DateTime _lastStateChangeTime = DateTime.MinValue;

    /// <summary>
    /// 窗口内容边距（与 XAML 中 MainBorder 的 Margin 一致）
    /// </summary>
    private const double ContentMargin = 4;

#endregion

#region Constructor

    public ControlBarWindow(ControlBarViewModel viewModel, PlayerWindow playerWindow)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _playerWindow = playerWindow ?? throw new ArgumentNullException(nameof(playerWindow));

        InitializeComponent();
        DataContext = _viewModel;

        InitializeWindowPosition();
        InitializeAutoShowHide();

        // 窗口失去激活状态时清除输入框焦点
        Deactivated += (s, e) =>
        {
            if (UrlTextBox.IsFocused)
            {
                // 使用 Dispatcher 延迟执行，确保在窗口状态更新后清除焦点
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                                       () =>
                                       {
                                           Keyboard.ClearFocus();
                                           // 移除输入框的焦点视觉样式
                                           FocusManager.SetFocusedElement(this, null);
                                       });
            }
        };

        // 窗口关闭时停止定时器
        Closing += (s, e) => StopAutoShowHide();

        // 订阅 ViewModel 属性变化事件以更新 UI
        SubscribeToViewModelChanges();
    }

    /// <summary>
    /// 订阅 ViewModel 属性变化事件（更新 UI 状态）
    /// </summary>
    private void SubscribeToViewModelChanges()
    {
        _viewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_viewModel.IsBookmarked):
                    UpdateBookmarkIcon();
                    break;
                case nameof(_viewModel.CurrentTitle):
                    // 标题变化时更新 ViewModel（用于收藏和记录笔记）
                    break;
            }
        };
    }

    /// <summary>
    /// 更新收藏按钮图标
    /// </summary>
    private void UpdateBookmarkIcon()
    {
        var textBlock = BtnBookmark.Content as System.Windows.Controls.TextBlock;
        if (textBlock != null)
        {
            textBlock.Text = _viewModel.IsBookmarked ? "★" : "☆";
        }
    }

#endregion

#region Properties

    /// <summary>
    /// 获取或设置当前 URL（桥接到 ViewModel）
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
        _mouseCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _mouseCheckTimer.Tick += MouseCheckTimer_Tick;

        // 初始化延迟隐藏定时器
        _hideDelayTimer =
            new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AppConstants.ControlBarHideDelayMs) };
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
        Win32Helper.SetToolWindowStyle(this);
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
    /// 拖动条鼠标移动：执行水平拖动（带吸附）
    /// </summary>
    private void DragBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var currentX = PointToScreen(e.GetPosition(this)).X;
            var deltaX = currentX - _dragStartX;

            // 计算新位置
            var newLeft = _windowStartLeft + deltaX;

            // 获取工作区
            var workArea = SystemParameters.WorkArea;

            // 限制在屏幕范围内
            newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - Width));

            // 水平吸附：左侧、居中、右侧
            const double snapThreshold = 20;

            // 计算吸附位置
            double leftSnapPos = workArea.Left;
            double centerSnapPos = workArea.Left + (workArea.Width - Width) / 2;
            double rightSnapPos = workArea.Right - Width;

            // 检查吸附
            if (Math.Abs(newLeft - leftSnapPos) <= snapThreshold)
            {
                newLeft = leftSnapPos;
            }
            else if (Math.Abs(newLeft - centerSnapPos) <= snapThreshold)
            {
                newLeft = centerSnapPos;
            }
            else if (Math.Abs(newLeft - rightSnapPos) <= snapThreshold)
            {
                newLeft = rightSnapPos;
            }

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
    /// URL 地址栏按键事件（UI 逻辑：快捷键处理）
    /// </summary>
    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // 调用 ViewModel 的导航命令
            _viewModel.NavigateCommand.Execute(_viewModel.CurrentUrl);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 菜单按钮点击（UI 逻辑：显示 ContextMenu）
    /// </summary>
    private void BtnMenu_Click(object sender, RoutedEventArgs e)
    {
        // 显示 ContextMenu
        if (BtnMenu.ContextMenu != null)
        {
            BtnMenu.ContextMenu.PlacementTarget = BtnMenu;
            BtnMenu.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BtnMenu.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// 历史记录菜单点击
    /// </summary>
    private void MenuHistory_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowHistoryCommand.Execute(null);
    }

    /// <summary>
    /// 收藏夹菜单点击
    /// </summary>
    private void MenuBookmarks_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowBookmarksCommand.Execute(null);
    }

    /// <summary>
    /// 开荒笔记菜单点击
    /// </summary>
    private void MenuPioneerNotes_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowPioneerNotesCommand.Execute(null);
    }

    /// <summary>
    /// 插件中心菜单点击
    /// </summary>
    private void MenuPluginCenter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowPluginCenterCommand.Execute(null);
    }

    /// <summary>
    /// 设置菜单点击
    /// </summary>
    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowSettingsCommand.Execute(null);
    }

    /// <summary>
    /// 关于菜单点击（保留本地处理，因为没有对应的 ViewModel 命令）
    /// </summary>
    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        aboutWindow.ShowDialog();
    }

#endregion

#region Auto Show / Hide Logic

    /// <summary>
    /// 鼠标位置检测定时器回调
    /// </summary>
    private void MouseCheckTimer_Tick(object? sender, EventArgs e)
    {
        // 防止在窗口关闭后操作
        if (!IsLoaded)
            return;

        // 拖动过程中不处理
        if (_isDragging)
            return;

        if (!Win32Helper.GetCursorPosition(out Win32Helper.POINT cursorPos))
            return;

        // 防抖：状态切换后短暂稳定期内不做处理
        if ((DateTime.Now - _lastStateChangeTime).TotalMilliseconds < AppConstants.ControlBarStateStabilityMs)
            return;

        // 使用物理像素计算触发区域（避免 DPI 问题）
        int screenHeight = Win32Helper.GetScreenMetrics(Win32Helper.SM_CYSCREEN);
        int triggerAreaHeight = (int)(screenHeight * AppConstants.ControlBarTriggerAreaRatio);

        // 获取窗口的屏幕坐标（物理像素）
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        bool isMouseOverWindow = false;
        bool isInWindowHorizontalRange = false;

        if (hwnd != IntPtr.Zero && Win32Helper.GetWindowRectangle(hwnd, out Win32Helper.RECT windowRect))
        {
            // 使用物理像素坐标进行比较
            isInWindowHorizontalRange =
                cursorPos.X >= windowRect.Left + ContentMargin && cursorPos.X <= windowRect.Right - ContentMargin;
            isMouseOverWindow = isInWindowHorizontalRange && cursorPos.Y >= windowRect.Top &&
                                cursorPos.Y <= windowRect.Bottom - ContentMargin;
        }

        // 检查鼠标是否在屏幕顶部触发区域（整个屏幕宽度，使用物理像素）
        bool isInTriggerArea = cursorPos.Y >= 0 && cursorPos.Y <= triggerAreaHeight;

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
            // 检查 ContextMenu 是否打开
            bool isContextMenuOpen = BtnMenu.ContextMenu?.IsOpen == true;

            if (isMouseOverWindow || isContextMenuOpen)
            {
                StopHideDelayTimer();
            }
            else if (UrlTextBox.IsFocused)
            {
                // 输入框聚焦但鼠标不在窗口上
                // 检测鼠标左键是否被按下（点击了其他位置）
                bool mouseButtonDown = Win32Helper.IsKeyPressed(Win32Helper.VK_LBUTTON);
                if (mouseButtonDown)
                {
                    // 点击了其他位置，清除焦点并移除焦点视觉样式
                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(this, null);
                }
                else
                {
                    // 输入框聚焦时不隐藏
                    StopHideDelayTimer();
                }
            }
            else
            {
                // 不在窗口上且输入框未聚焦，启动延迟隐藏
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
        if (!Win32Helper.GetCursorPosition(out Win32Helper.POINT cursorPos))
        {
            SetDisplayState(ControlBarDisplayState.Hidden);
            return;
        }

        // 获取窗口的屏幕坐标（物理像素）
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        bool isMouseOverWindow = false;

        if (hwnd != IntPtr.Zero && Win32Helper.GetWindowRectangle(hwnd, out Win32Helper.RECT windowRect))
        {
            bool isInWindowHorizontalRange =
                cursorPos.X >= windowRect.Left + ContentMargin && cursorPos.X <= windowRect.Right - ContentMargin;
            isMouseOverWindow = isInWindowHorizontalRange && cursorPos.Y >= windowRect.Top &&
                                cursorPos.Y <= windowRect.Bottom - ContentMargin;
        }

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
        // 防止在窗口关闭后操作
        if (!IsLoaded)
            return;

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
            Height = AppConstants.ControlBarTriggerLineHeight;
            Hide();
            break;

        case ControlBarDisplayState.TriggerLine:
            // 先确保主容器不可见（使用 Opacity 立即生效）
            MainBorder.Opacity = 0;
            MainBorder.Visibility = Visibility.Collapsed;
            TriggerLineBorder.Visibility = Visibility.Collapsed;
            // 设置高度
            Height = AppConstants.ControlBarTriggerLineHeight;
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
            Height = AppConstants.ControlBarExpandedHeight;
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

#region Public Control Methods

    /// <summary>
    /// 更新当前标题（从 PlayerWindow 获取，用于收藏和记录笔记）
    /// </summary>
    public void UpdateCurrentTitle(string title)
    {
        _viewModel.CurrentTitle = title;
    }

    /// <summary>
    /// 启动自动显示/隐藏监听
    /// </summary>
    public void StartAutoShowHide()
    {
        // 先显示窗口以创建 hwnd，然后再隐藏
        Show();
        SetDisplayState(ControlBarDisplayState.Hidden);
        _mouseCheckTimer?.Start();
    }

    /// <summary>
    /// 停止自动显示/隐藏监听
    /// </summary>
    public void StopAutoShowHide()
    {
        if (_mouseCheckTimer != null)
        {
            _mouseCheckTimer.Stop();
            _mouseCheckTimer.Tick -= MouseCheckTimer_Tick;
            _mouseCheckTimer = null;
        }

        if (_hideDelayTimer != null)
        {
            _hideDelayTimer.Stop();
            _hideDelayTimer.Tick -= HideDelayTimer_Tick;
            _hideDelayTimer = null;
        }
    }

#endregion
}
}
