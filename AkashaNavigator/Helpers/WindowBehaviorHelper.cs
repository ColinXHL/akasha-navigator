using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AkashaNavigator.Models.Config;
using Serilog;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 窗口行为辅助类
/// 提取窗口边缘吸附和透明度控制逻辑
/// </summary>
public class WindowBehaviorHelper
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "WindowBehaviorHelper");
#region Fields

    private readonly Window _window;
    private AppConfig _config;

    // 拖动相关
    private Win32Helper.POINT _dragOffset;
    private bool _isDragging;

    // 透明度相关
    private double _windowOpacity = 1.0;
    private bool _isClickThrough;
    private bool _isAutoClickThrough; // 自动点击穿透状态（插件控制）
    private double _opacityBeforeClickThrough = 1.0;
    private bool _isCursorInWindowWhileClickThrough;
    private DispatcherTimer? _clickThroughTimer;

    private bool _clickThroughSuspendedByMaximize;

#endregion

#region Constructor

    /// <summary>
    /// 创建窗口行为辅助类实例
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="config">应用配置</param>
    /// <param name="initialOpacity">初始透明度</param>
    public WindowBehaviorHelper(Window window, AppConfig config, double initialOpacity = 1.0)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _windowOpacity = initialOpacity;
    }

#endregion

#region Properties

    /// <summary>
    /// 当前窗口透明度
    /// </summary>
    public double WindowOpacity => _windowOpacity;

    /// <summary>
    /// 是否处于鼠标穿透模式（手动）
    /// </summary>
    public bool IsClickThrough => _isClickThrough;

    /// <summary>
    /// 是否处于自动鼠标穿透模式（插件控制）
    /// </summary>
    public bool IsAutoClickThrough => _isAutoClickThrough;

    /// <summary>
    /// 有效的点击穿透状态（手动 OR 自动）
    /// </summary>
    public bool IsEffectiveClickThrough => _isClickThrough || _isAutoClickThrough;

    /// <summary>
    /// 获取当前透明度百分比
    /// 穿透模式下返回保存的透明度设置，非穿透模式下返回当前窗口透明度
    /// </summary>
    public int OpacityPercent =>
        _isClickThrough ? (int)(_opacityBeforeClickThrough * 100) : (int)(_windowOpacity * 100);

#endregion

#region Public Methods - Configuration

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="config">新配置</param>
    public void UpdateConfig(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 设置初始透明度
    /// </summary>
    /// <param name="opacity">透明度值</param>
    public void SetInitialOpacity(double opacity)
    {
        _windowOpacity = Math.Clamp(opacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);
    }

#endregion

#region Public Methods - Opacity Control

    /// <summary>
    /// 降低透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double DecreaseOpacity()
    {
        if (_isClickThrough)
        {
            // 穿透模式下：修改保存的透明度设置
            _opacityBeforeClickThrough =
                Math.Max(AppConstants.MinOpacity, _opacityBeforeClickThrough - AppConstants.OpacityStep);
            return _opacityBeforeClickThrough;
        }

        // 非穿透模式：直接修改当前透明度
        _windowOpacity = Math.Max(AppConstants.MinOpacity, _windowOpacity - AppConstants.OpacityStep);
        System.Diagnostics.Debug.WriteLine(
            $"[WindowBehaviorHelper] DecreaseOpacity: setting opacity to {_windowOpacity}");
        Win32Helper.SetWindowOpacity(_window, _windowOpacity);
        return _windowOpacity;
    }

    /// <summary>
    /// 增加透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double IncreaseOpacity()
    {
        if (_isClickThrough)
        {
            // 穿透模式下：修改保存的透明度设置
            _opacityBeforeClickThrough =
                Math.Min(AppConstants.MaxOpacity, _opacityBeforeClickThrough + AppConstants.OpacityStep);
            return _opacityBeforeClickThrough;
        }

        // 非穿透模式：直接修改当前透明度
        _windowOpacity = Math.Min(AppConstants.MaxOpacity, _windowOpacity + AppConstants.OpacityStep);
        Win32Helper.SetWindowOpacity(_window, _windowOpacity);
        return _windowOpacity;
    }

    /// <summary>
    /// 应用当前透明度到窗口
    /// </summary>
    public void ApplyOpacity()
    {
        if (_windowOpacity < AppConstants.MaxOpacity)
        {
            Win32Helper.SetWindowOpacity(_window, _windowOpacity);
        }
    }

    /// <summary>
    /// 设置透明度到指定值
    /// </summary>
    /// <param name="opacity">透明度值 (0.0-1.0)</param>
    public void SetOpacity(double opacity)
    {
        opacity = Math.Clamp(opacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);

        if (_isClickThrough)
        {
            // 穿透模式下：修改保存的透明度设置
            _opacityBeforeClickThrough = opacity;
        }
        else
        {
            // 非穿透模式：直接修改当前透明度
            _windowOpacity = opacity;
            Win32Helper.SetWindowOpacity(_window, _windowOpacity);
        }
    }

#endregion

#region Public Methods - Click Through

    /// <summary>
    /// 切换鼠标穿透模式（手动）
    /// </summary>
    /// <returns>是否处于手动穿透模式</returns>
    public bool ToggleClickThrough()
    {
        _isClickThrough = !_isClickThrough;
        Logger.Debug("ToggleClickThrough: _isClickThrough={IsClickThrough}, _isAutoClickThrough={IsAutoClickThrough}",
                     _isClickThrough, _isAutoClickThrough);

        if (_isClickThrough)
        {
            // 手动穿透启用，保存当前透明度（如果自动穿透未启用）
            if (!_isAutoClickThrough)
            {
                _opacityBeforeClickThrough = _windowOpacity;
            }
        }

        // 根据有效穿透状态更新窗口
        UpdateEffectiveClickThrough();

        return _isClickThrough;
    }

    /// <summary>
    /// 停止穿透模式定时器（用于窗口关闭时清理）
    /// </summary>
    public void StopClickThroughTimer()
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
    /// 全屏时暂停的自动穿透状态
    /// </summary>
    private bool _autoClickThroughSuspendedByMaximize;

    /// <summary>
    /// 全屏前保存的透明度（用于恢复后重新检测）
    /// </summary>
    private double _opacityBeforeMaximize = 1.0;

    /// <summary>
    /// 全屏时暂停穿透和透明度控制
    /// </summary>
    public void SuspendClickThroughForMaximize()
    {
        Logger.Debug("SuspendClickThroughForMaximize: manual={Manual}, auto={Auto}", _isClickThrough,
                     _isAutoClickThrough);

        // 保存当前状态
        _clickThroughSuspendedByMaximize = _isClickThrough;
        _autoClickThroughSuspendedByMaximize = _isAutoClickThrough;
        _opacityBeforeMaximize = _opacityBeforeClickThrough;

        // 停止定时器
        StopClickThroughTimer();

        // 禁用自动穿透（不触发 UpdateEffectiveClickThrough）
        _isAutoClickThrough = false;

        // 恢复透明度为 1
        _windowOpacity = 1.0;
        Win32Helper.SetWindowOpacity(_window, 1.0);

        // 禁用穿透
        if (_isClickThrough || _autoClickThroughSuspendedByMaximize)
        {
            Win32Helper.SetClickThrough(_window, false);
        }

        Logger.Debug("SuspendClickThroughForMaximize: opacity set to 1.0, click-through disabled");
    }

    /// <summary>
    /// 还原窗口时恢复穿透模式
    /// 注意：自动穿透的恢复由 CursorDetectionService.Resume() 触发
    /// </summary>
    public void ResumeClickThroughAfterRestore()
    {
        Logger.Debug("ResumeClickThroughAfterRestore: savedManual={Manual}, savedAuto={Auto}",
                     _clickThroughSuspendedByMaximize, _autoClickThroughSuspendedByMaximize);

        // 恢复保存的透明度设置
        _opacityBeforeClickThrough = _opacityBeforeMaximize;

        // 只恢复手动穿透模式
        // 自动穿透由 CursorDetectionService.Resume() -> CheckInitialState() -> 插件事件 -> SetAutoClickThrough 恢复
        if (_clickThroughSuspendedByMaximize)
        {
            // 重新启用手动穿透
            Win32Helper.SetClickThrough(_window, true);

            // 启动定时器
            StartClickThroughTimer();
        }

        // 重置暂停标记
        _clickThroughSuspendedByMaximize = false;
        _autoClickThroughSuspendedByMaximize = false;

        Logger.Debug("ResumeClickThroughAfterRestore: manual click-through restored={Restored}", _isClickThrough);
    }

    /// <summary>
    /// 设置自动点击穿透状态（由插件控制）
    /// </summary>
    /// <param name="enabled">是否启用自动穿透</param>
    public void SetAutoClickThrough(bool enabled)
    {
        if (_isAutoClickThrough == enabled)
            return;

        _isAutoClickThrough = enabled;
        Logger.Debug("SetAutoClickThrough: _isAutoClickThrough={IsAutoClickThrough}", _isAutoClickThrough);

        // 根据有效穿透状态更新窗口
        UpdateEffectiveClickThrough();
    }

    /// <summary>
    /// 重置自动点击穿透状态（插件卸载或禁用时调用）
    /// </summary>
    public void ResetAutoClickThrough()
    {
        if (!_isAutoClickThrough)
            return;

        _isAutoClickThrough = false;
        Logger.Debug("ResetAutoClickThrough: _isAutoClickThrough reset to false");

        // 根据有效穿透状态更新窗口
        UpdateEffectiveClickThrough();
    }

#endregion

#region Public Methods - Edge Snapping

    /// <summary>
    /// 处理窗口开始移动/调整大小
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public void HandleEnterSizeMove(IntPtr hwnd)
    {
        if (Win32Helper.GetCursorPosition(out var cursorPos) &&
            Win32Helper.GetWindowRectangle(hwnd, out var windowRect))
        {
            _dragOffset.X = cursorPos.X - windowRect.Left;
            _dragOffset.Y = cursorPos.Y - windowRect.Top;
            _isDragging = true;
        }
    }

    /// <summary>
    /// 处理窗口结束移动/调整大小
    /// </summary>
    public void HandleExitSizeMove()
    {
        _isDragging = false;
    }

    /// <summary>
    /// 处理窗口移动时的边缘吸附
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="lParam">RECT 指针</param>
    public void HandleWindowMoving(IntPtr hwnd, IntPtr lParam)
    {
        if (!_isDragging || lParam == IntPtr.Zero)
            return;

        // 获取当前鼠标位置
        if (!Win32Helper.GetCursorPosition(out var cursorPos))
            return;

        // 获取 DPI 缩放比例
        var source = PresentationSource.FromVisual(_window);
        double dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        // 计算物理像素阈值（使用配置值）
        int snapThreshold = _config.EnableEdgeSnap ? _config.SnapThreshold : 0;
        int threshold = (int)(snapThreshold * dpiScale);

        // 获取工作区（物理像素）- 排除任务栏
        var workAreaWpf = SystemParameters.WorkArea;
        var workArea = Win32Helper.ToPhysicalRect(workAreaWpf, dpiScale);

        // 获取屏幕完整区域（物理像素）- 包括任务栏
        var screenRect =
            new Win32Helper.RECT { Left = 0, Top = 0, Right = (int)(SystemParameters.PrimaryScreenWidth * dpiScale),
                                   Bottom = (int)(SystemParameters.PrimaryScreenHeight * dpiScale) };

        // 获取窗口当前大小
        var rect = Marshal.PtrToStructure<Win32Helper.RECT>(lParam);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        // 根据鼠标位置和偏移计算用户意图的窗口位置
        int intendedLeft = cursorPos.X - _dragOffset.X;
        int intendedTop = cursorPos.Y - _dragOffset.Y;

        // 对意图位置进行吸附计算
        int finalLeft = intendedLeft;
        int finalTop = intendedTop;

        // 左边缘吸附（工作区和屏幕边缘相同）
        if (Math.Abs(intendedLeft - workArea.Left) <= threshold)
        {
            finalLeft = workArea.Left;
        }
        // 右边缘吸附（工作区和屏幕边缘相同）
        else if (Math.Abs(intendedLeft + width - workArea.Right) <= threshold)
        {
            finalLeft = workArea.Right - width;
        }

        // 上边缘吸附（工作区）
        if (Math.Abs(intendedTop - workArea.Top) <= threshold)
        {
            finalTop = workArea.Top;
        }
        // 下边缘吸附 - 优先工作区（任务栏上方）
        else if (Math.Abs(intendedTop + height - workArea.Bottom) <= threshold)
        {
            finalTop = workArea.Bottom - height;
        }
        // 下边缘吸附 - 屏幕真实底部
        else if (Math.Abs(intendedTop + height - screenRect.Bottom) <= threshold)
        {
            finalTop = screenRect.Bottom - height;
        }

        // 更新窗口位置
        rect.Left = finalLeft;
        rect.Top = finalTop;
        rect.Right = finalLeft + width;
        rect.Bottom = finalTop + height;

        Marshal.StructureToPtr(rect, lParam, false);
    }

    /// <summary>
    /// 处理窗口调整大小时的边缘吸附
    /// </summary>
    /// <param name="wParam">调整方向</param>
    /// <param name="lParam">RECT 指针</param>
    public void HandleWindowSizing(IntPtr wParam, IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
            return;

        // 获取 DPI 缩放比例
        var source = PresentationSource.FromVisual(_window);
        double dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        // 计算物理像素阈值（使用配置值）
        int snapThreshold = _config.EnableEdgeSnap ? _config.SnapThreshold : 0;
        int threshold = (int)(snapThreshold * dpiScale);

        // 获取工作区（物理像素）
        var workAreaWpf = SystemParameters.WorkArea;
        var workArea = Win32Helper.ToPhysicalRect(workAreaWpf, dpiScale);

        int sizingEdge = wParam.ToInt32();
        var rect = Marshal.PtrToStructure<Win32Helper.RECT>(lParam);
        Win32Helper.SnapSizingEdge(ref rect, workArea, threshold, sizingEdge);
        Marshal.StructureToPtr(rect, lParam, false);
    }

#endregion

#region Private Methods

    /// <summary>
    /// 启动穿透模式鼠标检测定时器
    /// </summary>
    private void StartClickThroughTimer()
    {
        _clickThroughTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _clickThroughTimer.Tick += ClickThroughTimer_Tick;
        _clickThroughTimer.Start();

        // 立即检测一次
        UpdateClickThroughOpacity();
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
    /// 注意：自动穿透模式下由插件控制透明度，此方法不做处理
    /// </summary>
    private void UpdateClickThroughOpacity()
    {
        // 自动穿透模式下，透明度由插件控制，不在这里处理
        if (_isAutoClickThrough)
        {
            return;
        }

        bool cursorInWindow = Win32Helper.IsCursorInWindow(_window);

        // 只有状态变化时才更新透明度
        if (cursorInWindow != _isCursorInWindowWhileClickThrough)
        {
            _isCursorInWindowWhileClickThrough = cursorInWindow;

            if (cursorInWindow)
            {
                // 鼠标进入窗口，降至最低透明度
                Win32Helper.SetWindowOpacity(_window, AppConstants.MinOpacity);
            }
            else
            {
                // 鼠标离开窗口，恢复正常透明度
                Win32Helper.SetWindowOpacity(_window, _opacityBeforeClickThrough);
            }
        }
    }

    /// <summary>
    /// 根据有效穿透状态更新窗口
    /// </summary>
    private void UpdateEffectiveClickThrough()
    {
        bool effectiveClickThrough = IsEffectiveClickThrough;
        Logger.Debug("UpdateEffectiveClickThrough: effective={Effective}, manual={Manual}, auto={Auto}",
                     effectiveClickThrough, _isClickThrough, _isAutoClickThrough);

        if (effectiveClickThrough)
        {
            // 如果之前没有穿透，需要保存透明度
            if (!_isClickThrough && _isAutoClickThrough)
            {
                // 自动穿透刚启用，保存当前透明度
                _opacityBeforeClickThrough = _windowOpacity;
            }

            // 只有手动穿透模式才启动定时器（根据鼠标位置调整透明度）
            // 自动穿透模式由插件完全控制透明度
            if (_isClickThrough && _clickThroughTimer == null)
            {
                StartClickThroughTimer();
            }

            // 启用穿透
            Win32Helper.SetClickThrough(_window, true);
        }
        else
        {
            // 停止定时器
            StopClickThroughTimer();

            // 只有手动穿透模式关闭时才恢复透明度
            // 自动穿透模式关闭时由插件控制透明度
            if (!_isAutoClickThrough)
            {
                _windowOpacity = _opacityBeforeClickThrough;
                Logger.Debug("UpdateEffectiveClickThrough: restoring opacity to {Opacity}", _windowOpacity);
                Win32Helper.SetWindowOpacity(_window, _windowOpacity);
            }

            // 禁用穿透
            Win32Helper.SetClickThrough(_window, false);
        }
    }

#endregion
}
}
