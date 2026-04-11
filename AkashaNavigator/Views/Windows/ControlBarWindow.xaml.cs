using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
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
/// </summary>
public partial class ControlBarWindow : Window
{

#region Fields

private readonly ControlBarViewModel _viewModel;
    private readonly PlayerWindow _playerWindow;
    private readonly ControlBarDisplayController _displayController;
    private readonly IEventBus _eventBus;

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

#endregion

#region Constructor

public ControlBarWindow(ControlBarViewModel viewModel,
                            PlayerWindow playerWindow,
                            ControlBarDisplayController displayController,
                            IEventBus eventBus)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _playerWindow = playerWindow ?? throw new ArgumentNullException(nameof(playerWindow));
        _displayController = displayController ?? throw new ArgumentNullException(nameof(displayController));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        InitializeComponent();
        DataContext = _viewModel;

        InitializeWindowPosition();
        InitializeAutoShowHide();

        // 订阅穿透状态变化事件（穿透模式激活时抑制控制栏自动显示）
        _eventBus.Subscribe<ClickThroughChangedEvent>(OnClickThroughChanged);

        // 订阅老板键隐藏模式变化事件（窗口隐藏时抑制控制栏自动显示）
        _eventBus.Subscribe<BossKeyHiddenModeChangedEvent>(OnBossKeyHiddenModeChanged);

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
            SetDisplayState(ControlBarDisplayState.Hidden);
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
            SetDisplayState(ControlBarDisplayState.Hidden);
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
    private void SetDisplayState(ControlBarDisplayState state)
    {
        // 防止在窗口关闭后操作
        if (!IsLoaded)
            return;

        if (_displayController.State == state)
            return;

        _displayController.SetState(state, DateTime.UtcNow);

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

        // 取消订阅穿透状态变化事件，防止内存泄漏
        _eventBus.Unsubscribe<ClickThroughChangedEvent>(OnClickThroughChanged);
    }

#endregion
}
}
