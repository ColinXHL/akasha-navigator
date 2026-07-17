using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
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
/// 支持多显示器：跟随播放器窗口所在显示器定位
/// </summary>
public partial class ControlBarWindow : Window
{

#region Fields

private readonly ControlBarViewModel _viewModel;
    private readonly PlayerWindow _playerWindow;
private readonly ControlBarDisplayController _displayController;
    private readonly IEventBus _eventBus;
    private readonly IMonitorLayoutService _monitorLayoutService;
    private readonly IWindowStateService _windowStateService;

    /// <summary>
    /// 是否正在拖动
    /// </summary>
    private bool _isDragging;

    /// <summary>
    /// 是否处于穿透抑制模式（穿透模式激活时，控制栏不响应鼠标）
    /// </summary>
    private bool _isClickThroughSuppressed;

    /// <summary>
    /// 是否处于老板键隐藏模式（窗口隐藏时，控制栏不得自动出现）
    /// </summary>
    private bool _isBossKeyHidden;

    /// <summary>
    /// 拖动起始点的 X 坐标（屏幕坐标）
    /// </summary>
    private double _dragStartX;

    /// <summary>
    /// 拖动起始时窗口的 Left 值
    /// </summary>
    private double _windowStartLeft;

    /// <summary>
    /// 鼠标位置检测定时器
    /// </summary>
    private DispatcherTimer? _mouseCheckTimer;

    /// <summary>
    /// 延迟隐藏定时器
    /// </summary>
    private DispatcherTimer? _hideDelayTimer;

    /// <summary>
    /// 窗口内容边距（与 XAML 中 MainBorder 的 Margin 一致）
    /// </summary>
    private const double ContentMargin = 4;

    /// <summary>
    /// 控制栏中心点在当前显示器工作区中的横向比例（0-1）
    /// 0.5 表示居中；拖动后会更新，用于跨显示器重新计算位置
    /// </summary>
    private double _centerAnchorRatio = 0.5;

    /// <summary>
    /// 是否已安排一次显示后的定位校正。
    /// WPF 在 Show() 后可能会根据自身 Left/Top/Width/Height 再应用一次窗口位置。
    /// </summary>
    private bool _isRepositionCorrectionScheduled;

#endregion

#region Constructor

public ControlBarWindow(ControlBarViewModel viewModel,
                             PlayerWindow playerWindow,
                             ControlBarDisplayController displayController,
                             IEventBus eventBus,
                             IMonitorLayoutService monitorLayoutService,
                             IWindowStateService windowStateService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _playerWindow = playerWindow ?? throw new ArgumentNullException(nameof(playerWindow));
        _displayController = displayController ?? throw new ArgumentNullException(nameof(displayController));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _monitorLayoutService = monitorLayoutService ?? throw new ArgumentNullException(nameof(monitorLayoutService));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));

        InitializeComponent();
        DataContext = _viewModel;

        LoadControlBarPositionState();
        InitializeAutoShowHide();

        // 订阅穿透状态变化事件（穿透模式激活时抑制控制栏自动显示）
        _eventBus.Subscribe<ClickThroughChangedEvent>(OnClickThroughChanged);

        // 订阅老板键隐藏模式变化事件（窗口隐藏时抑制控制栏自动显示）
        _eventBus.Subscribe<BossKeyHiddenModeChangedEvent>(OnBossKeyHiddenModeChanged);

        // 订阅播放器显示器变化事件，跟随播放器移动到其他显示器
        _eventBus.Subscribe<PlayerMonitorChangedEvent>(OnPlayerMonitorChanged);

        // 订阅显示拓扑变化事件（显示器热插拔）
        _eventBus.Subscribe<DisplayTopologyChangedEvent>(OnDisplayTopologyChanged);

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

        // 窗口关闭时停止定时器并持久化控制栏位置
        Closing += (s, e) =>
        {
            SaveControlBarState(updateAnchorFromWindowPosition: true);
            StopAutoShowHide();
        };

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
    /// 加载控制栏的横向锚点。
    /// 实际窗口定位延迟到 SourceInitialized，此时播放器和控制栏句柄均已创建。
    /// </summary>
    private void LoadControlBarPositionState()
    {
        var state = _windowStateService.Load();
        if (state.ControlBarPositionVersion < AppConstants.ControlBarPositionVersion)
        {
            _centerAnchorRatio = 0.5;
            _windowStateService.Update(current =>
            {
                current.ControlBarCenterAnchorRatio = _centerAnchorRatio;
                current.ControlBarPositionVersion = AppConstants.ControlBarPositionVersion;
            });
            return;
        }

        _centerAnchorRatio = Math.Clamp(state.ControlBarCenterAnchorRatio, 0.0, 1.0);
    }

    /// <summary>
    /// 播放器显示器变化回调
    /// 当播放器移动到其他显示器时，控制栏跟随到同一显示器
    /// </summary>
    private void OnPlayerMonitorChanged(PlayerMonitorChangedEvent e)
    {
        // 确保在 UI 线程执行
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnPlayerMonitorChanged(e));
            return;
        }

        if (e.MonitorInfo == null)
            return;

        ApplyAnchorToMonitor(e.MonitorInfo);
        SaveControlBarState(e.MonitorInfo, updateAnchorFromWindowPosition: false);
    }

    /// <summary>
    /// 显示拓扑变化回调
    /// 处理显示器热插拔（连接/断开）
    /// </summary>
    private void OnDisplayTopologyChanged(DisplayTopologyChangedEvent e)
    {
        // 确保在 UI 线程执行
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnDisplayTopologyChanged(e));
            return;
        }

        // 重新定位，确保控制栏在有效显示器上
        RepositionToPlayerMonitor();
    }

    /// <summary>
    /// 将控制栏重新定位到播放器所在的显示器
    /// </summary>
    private void RepositionToPlayerMonitor(bool saveState = true)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var monitor = _monitorLayoutService.GetMonitorFromWindowOrDefault(hwnd);
        ApplyAnchorToMonitor(monitor);
        if (saveState)
        {
            SaveControlBarState(monitor, updateAnchorFromWindowPosition: false);
        }
    }

    /// <summary>
    /// 在 WPF 完成 Show/Layout 后再次校正位置，防止显示流程恢复旧窗口坐标。
    /// </summary>
    private void ScheduleRepositionCorrection()
    {
        if (_isRepositionCorrectionScheduled)
            return;

        _isRepositionCorrectionScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _isRepositionCorrectionScheduled = false;
            if (IsLoaded)
            {
                RepositionToPlayerMonitor(saveState: false);
            }
        }));
    }

    /// <summary>
    /// 获取指定显示器对应的 WPF 工作区坐标
    /// 优先使用播放器窗口的 DPI，上屏切换时更稳定
    /// </summary>
    private double GetPlayerDpiScale()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
        var win32DpiScale = Win32Helper.GetDpiScaleForWindow(hwnd);
        if (win32DpiScale > 0)
        {
            return win32DpiScale;
        }

        var playerSource = PresentationSource.FromVisual(_playerWindow);
        return playerSource?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    /// <summary>
    /// 根据已保存的中心锚点比例，将控制栏定位到目标显示器（以物理像素为基准，避免累计偏移）
    /// </summary>
    private void ApplyAnchorToMonitor(MonitorInfo monitor)
    {
        double dpiScale = GetPlayerDpiScale();
        double heightDip = _displayController.State == ControlBarDisplayState.Expanded
            ? AppConstants.ControlBarExpandedHeight
            : AppConstants.ControlBarTriggerLineHeight;
        var bounds =
            ControlBarBoundsCalculator.Calculate(monitor, dpiScale, _centerAnchorRatio, heightDip);

        ApplyPhysicalBounds(bounds);
    }

    /// <summary>
    /// 使用单一物理像素坐标系应用窗口位置和大小
    /// </summary>
    private void ApplyPhysicalBounds(Win32Helper.RECT bounds)
    {
        SyncWpfBoundsFromPhysicalBounds(bounds);

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            Win32Helper.SetWindowRectangle(hwnd,
                                           bounds.Left,
                                           bounds.Top,
                                           bounds.Right - bounds.Left,
                                           bounds.Bottom - bounds.Top);
            return;
        }
    }

    /// <summary>
    /// 同步 WPF 逻辑坐标，避免 Show()/Hide() 使用旧的 Left/Top/Width/Height 重新覆盖 Win32 位置。
    /// </summary>
    private void SyncWpfBoundsFromPhysicalBounds(Win32Helper.RECT bounds)
    {
        double dpiScale = GetPlayerDpiScale();
        Left = bounds.Left / dpiScale;
        Top = bounds.Top / dpiScale;
        Width = (bounds.Right - bounds.Left) / dpiScale;
        Height = (bounds.Bottom - bounds.Top) / dpiScale;
    }

    /// <summary>
    /// 根据当前窗口物理位置更新中心锚点比例，用于跨显示器保持相对位置
    /// </summary>
    private void UpdateAnchorFromCurrentPosition(MonitorInfo monitor)
    {
        double workAreaWidthPx = monitor.WorkAreaRect.Right - monitor.WorkAreaRect.Left;
        if (workAreaWidthPx <= 0)
        {
            _centerAnchorRatio = 0.5;
            return;
        }

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && Win32Helper.GetWindowRectangle(hwnd, out Win32Helper.RECT rect))
        {
            double centerXPx = rect.Left + ((rect.Right - rect.Left) / 2.0);
            _centerAnchorRatio = Math.Clamp((centerXPx - monitor.WorkAreaRect.Left) / workAreaWidthPx, 0.0, 1.0);
            return;
        }
    }

    /// <summary>
    /// 保存控制栏锚点和显示器信息
    /// </summary>
    private void SaveControlBarState(MonitorInfo? monitor = null, bool updateAnchorFromWindowPosition = true)
    {
        monitor ??= ResolveCurrentControlBarMonitor();
        if (monitor == null)
            return;

        if (updateAnchorFromWindowPosition)
        {
            UpdateAnchorFromCurrentPosition(monitor);
        }

        _windowStateService.Update(state =>
        {
            state.ControlBarCenterAnchorRatio = _centerAnchorRatio;
            state.ControlBarPositionVersion = AppConstants.ControlBarPositionVersion;
            state.ControlBarMonitorDeviceName = monitor.DeviceName;
        });
    }

    /// <summary>
    /// 获取当前控制栏所在显示器，失败时回退到播放器显示器
    /// </summary>
    private MonitorInfo? ResolveCurrentControlBarMonitor()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            return _monitorLayoutService.GetMonitorFromWindow(hwnd);
        }

        var playerHwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
        if (playerHwnd != IntPtr.Zero)
        {
            return _monitorLayoutService.GetMonitorFromWindowOrDefault(playerHwnd);
        }

        return _monitorLayoutService.GetPrimaryMonitor();
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

        // 句柄和 DPI 均已可用，使用播放器当前显示器完成首次定位。
        RepositionToPlayerMonitor();
        ScheduleRepositionCorrection();
    }

    /// <summary>
    /// 拖动条鼠标按下：开始拖动
    /// </summary>
    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            _isDragging = true;

            if (!Win32Helper.GetCursorPosition(out Win32Helper.POINT cursorPoint))
                return;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || !Win32Helper.GetWindowRectangle(hwnd, out Win32Helper.RECT rect))
                return;

            _dragStartX = cursorPoint.X;
            _windowStartLeft = rect.Left;

            // 捕获鼠标
            Mouse.Capture(DragBar);

            // 注册鼠标移动和释放事件
            DragBar.MouseMove += DragBar_MouseMove;
            DragBar.MouseLeftButtonUp += DragBar_MouseLeftButtonUp;
        }
    }

    /// <summary>
    /// 拖动条鼠标移动：执行水平拖动（带吸附，多显示器感知）
    /// </summary>
    private void DragBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            if (!Win32Helper.GetCursorPosition(out Win32Helper.POINT cursorPoint))
                return;

            var deltaX = cursorPoint.X - _dragStartX;

            // 计算新位置
            var newLeft = _windowStartLeft + deltaX;

            // 获取播放器所在显示器的工作区
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
            Win32Helper.RECT workArea;
            if (hwnd != IntPtr.Zero)
            {
                var monitor = _monitorLayoutService.GetMonitorFromWindowOrDefault(hwnd);
                workArea = monitor.WorkAreaRect;
            }
            else
            {
                var primary = _monitorLayoutService.GetPrimaryMonitor();
                workArea = primary.WorkAreaRect;
            }

            var controlBarHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (controlBarHwnd == IntPtr.Zero || !Win32Helper.GetWindowRectangle(controlBarHwnd, out Win32Helper.RECT currentRect))
                return;

            double currentWidthPx = currentRect.Right - currentRect.Left;
            double currentHeightPx = currentRect.Bottom - currentRect.Top;

            // 限制在显示器工作区范围内
            newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - currentWidthPx));

            // 水平吸附：左侧、居中、右侧
            double snapThreshold = 20 * GetPlayerDpiScale();

            // 计算吸附位置
            double leftSnapPos = workArea.Left;
            double centerSnapPos = workArea.Left + ((workArea.Right - workArea.Left) - currentWidthPx) / 2;
            double rightSnapPos = workArea.Right - currentWidthPx;

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

            Win32Helper.SetWindowRectangle(controlBarHwnd,
                                           (int)Math.Round(newLeft),
                                           currentRect.Top,
                                           (int)Math.Round(currentWidthPx),
                                           (int)Math.Round(currentHeightPx));
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

            var playerHwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
            if (playerHwnd != IntPtr.Zero)
            {
                var monitor = _monitorLayoutService.GetMonitorFromWindowOrDefault(playerHwnd);
                SaveControlBarState(monitor, updateAnchorFromWindowPosition: true);
            }

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

#region Click-Through Suppression

    /// <summary>
    /// 穿透状态变化事件处理
    /// 穿透模式激活时隐藏控制栏并停止检测；穿透模式关闭时恢复自动显示
    /// </summary>
    private void OnClickThroughChanged(ClickThroughChangedEvent e)
    {
        // 确保在 UI 线程执行
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnClickThroughChanged(e));
            return;
        }

        if (e.IsEffectiveClickThrough)
        {
            // 穿透模式激活：抑制控制栏自动显示
            _isClickThroughSuppressed = true;
            _hideDelayTimer?.Stop();
            SetDisplayState(ControlBarDisplayState.Hidden, force: true);
        }
        else
        {
            // 穿透模式关闭：恢复控制栏正常行为
            _isClickThroughSuppressed = false;
            // 重置显示控制器状态，避免残留状态影响恢复后的行为
            _displayController.SetState(ControlBarDisplayState.Hidden, DateTime.UtcNow);
            _mouseCheckTimer?.Start();
        }
    }

    /// <summary>
    /// 老板键隐藏模式变化事件处理
    /// 窗口隐藏时强制隐藏控制栏并停止检测；窗口显示时恢复控制栏正常行为
    /// </summary>
    private void OnBossKeyHiddenModeChanged(BossKeyHiddenModeChangedEvent e)
    {
        // 确保在 UI 线程执行
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnBossKeyHiddenModeChanged(e));
            return;
        }

        if (e.IsHidden)
        {
            // 老板键隐藏模式激活：强制隐藏控制栏并停止自动检测
            _isBossKeyHidden = true;
            _hideDelayTimer?.Stop();
            SetDisplayState(ControlBarDisplayState.Hidden, force: true);
        }
        else
        {
            // 老板键隐藏模式关闭：恢复控制栏正常行为
            _isBossKeyHidden = false;
            // 重置显示控制器状态，避免残留状态影响恢复后的行为
            _displayController.SetState(ControlBarDisplayState.Hidden, DateTime.UtcNow);
            _mouseCheckTimer?.Start();
        }
    }

    #endregion

    #region Auto Show / Hide Logic

/// <summary>
    /// 鼠标位置检测定时器回调
    /// 多显示器感知：触发区域基于播放器所在显示器的完整区域
    /// </summary>
    private void MouseCheckTimer_Tick(object? sender, EventArgs e)
    {
        // 防止在窗口关闭后操作
        if (!IsLoaded)
            return;

        // 老板键隐藏模式下不处理鼠标检测
        if (_isBossKeyHidden)
            return;

        // 穿透抑制模式下不处理鼠标检测
        if (_isClickThroughSuppressed)
            return;

        // 拖动过程中不处理
        if (_isDragging)
            return;

        if (!Win32Helper.GetCursorPosition(out Win32Helper.POINT cursorPos))
            return;

        // 使用播放器所在显示器的完整区域计算触发区域（避免 DPI 问题）
        var playerHwnd = new System.Windows.Interop.WindowInteropHelper(_playerWindow).Handle;
        MonitorInfo? playerMonitor = null;
        if (playerHwnd != IntPtr.Zero)
        {
            playerMonitor = _monitorLayoutService.GetMonitorFromWindow(playerHwnd);
        }
        playerMonitor ??= _monitorLayoutService.GetPrimaryMonitor();

        // 触发区域：显示器顶部的一定高度
        int triggerAreaHeight = (int)((playerMonitor.MonitorRect.Bottom - playerMonitor.MonitorRect.Top) * AppConstants.ControlBarTriggerAreaRatio);

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

        // 检查鼠标是否在播放器显示器顶部的触发区域（整个显示器宽度，使用物理像素）
        bool isInTriggerArea = cursorPos.X >= playerMonitor.MonitorRect.Left &&
                               cursorPos.X <= playerMonitor.MonitorRect.Right &&
                               cursorPos.Y >= playerMonitor.MonitorRect.Top &&
                               cursorPos.Y <= playerMonitor.MonitorRect.Top + triggerAreaHeight;

        bool isContextMenuOpen = BtnMenu.ContextMenu?.IsOpen == true;
        bool isUrlTextBoxFocused = UrlTextBox.IsFocused;

        if (isUrlTextBoxFocused && !isMouseOverWindow && !isContextMenuOpen)
        {
            bool mouseButtonDown = Win32Helper.IsKeyPressed(Win32Helper.VK_LBUTTON);
            if (mouseButtonDown)
            {
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, null);
                isUrlTextBoxFocused = false;
            }
        }

        var decision = _displayController.EvaluateMouse(
            isMouseOverWindow,
            isInTriggerArea,
            isContextMenuOpen,
            isUrlTextBoxFocused,
            DateTime.UtcNow);

        ApplyDecision(decision);
    }

    private void ApplyDecision(ControlBarDecision decision)
    {
        if (decision.StopHideDelayTimer)
        {
            StopHideDelayTimer();
        }

        if (decision.StartHideDelayTimer)
        {
            StartHideDelayTimer();
        }

        if (decision.NextState != _displayController.State)
        {
            SetDisplayState(decision.NextState);
        }
    }

    /// <summary>
    /// 延迟隐藏定时器回调
    /// </summary>
    private void HideDelayTimer_Tick(object? sender, EventArgs e)
    {
        _hideDelayTimer?.Stop();

        // 老板键隐藏模式下直接停止并返回
        if (_isBossKeyHidden)
            return;

        // 穿透抑制模式下直接停止并返回
        if (_isClickThroughSuppressed)
            return;

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

        bool isContextMenuOpen = BtnMenu.ContextMenu?.IsOpen == true;
        bool isUrlTextBoxFocused = UrlTextBox.IsFocused;

        var decision = _displayController.EvaluateHideDelay(
            isMouseOverWindow,
            isMouseInTopTriggerZone: false,
            isContextMenuOpen,
            isUrlTextBoxFocused,
            DateTime.UtcNow);

        ApplyDecision(decision);
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
    private void SetDisplayState(ControlBarDisplayState state, bool force = false)
    {
        // 防止在窗口关闭后操作
        if (!IsLoaded)
            return;

        if (_displayController.State == state && !force)
            return;

        _displayController.SetState(state, DateTime.UtcNow);

        switch (state)
        {
        case ControlBarDisplayState.Hidden:
            // 重置为 TriggerLine 状态的视觉效果，避免下次显示时闪烁
            MainBorder.Opacity = 0;
            MainBorder.Visibility = Visibility.Collapsed;
            TriggerLineBorder.Visibility = Visibility.Collapsed;
            RepositionToPlayerMonitor(saveState: false);
            Hide();
            break;
        case ControlBarDisplayState.TriggerLine:
            // 先确保主容器不可见（使用 Opacity 立即生效）
            MainBorder.Opacity = 0;
            MainBorder.Visibility = Visibility.Collapsed;
            TriggerLineBorder.Visibility = Visibility.Collapsed;
            // 显示触发线
            TriggerLineBorder.Visibility = Visibility.Visible;
            if (!IsVisible)
            {
                Show();
            }
            RepositionToPlayerMonitor(saveState: false);
            ScheduleRepositionCorrection();
            break;

        case ControlBarDisplayState.Expanded:
            // 先隐藏触发线
            TriggerLineBorder.Visibility = Visibility.Collapsed;
            // 显示主容器（先设置 Opacity 为 0，再设置 Visibility，最后恢复 Opacity）
            MainBorder.Opacity = 0;
            MainBorder.Visibility = Visibility.Visible;
            MainBorder.Opacity = 1;
            if (!IsVisible)
            {
                Show();
            }
            RepositionToPlayerMonitor(saveState: false);
            ScheduleRepositionCorrection();
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
        SetDisplayState(ControlBarDisplayState.Hidden, force: true);
        ScheduleRepositionCorrection();
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

        // 取消订阅穿透状态变化事件，防止内存泄漏
        _eventBus.Unsubscribe<ClickThroughChangedEvent>(OnClickThroughChanged);
        _eventBus.Unsubscribe<PlayerMonitorChangedEvent>(OnPlayerMonitorChanged);
        _eventBus.Unsubscribe<DisplayTopologyChangedEvent>(OnDisplayTopologyChanged);
    }

#endregion
}
}
