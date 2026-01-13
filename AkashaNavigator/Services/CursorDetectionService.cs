using System;
using System.Collections.Generic;
using System.Windows.Threading;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.Services
{
/// <summary>
/// 鼠标光标检测服务
/// 用于检测游戏内鼠标是否显示（如打开地图/菜单时）
/// </summary>
public class CursorDetectionService : ICursorDetectionService, IDisposable
{
#region Singleton

    private static ICursorDetectionService? _instance;

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static ICursorDetectionService Instance
    {
        get {
            if (_instance == null)
            {
                _instance = new CursorDetectionService();
            }
            return _instance;
        }
        set => _instance = value;
    }

#endregion

#region Events

    /// <summary>
    /// 鼠标从隐藏变为显示时触发
    /// </summary>
    public event EventHandler? CursorShown;

    /// <summary>
    /// 鼠标从显示变为隐藏时触发
    /// </summary>
    public event EventHandler? CursorHidden;

#endregion

#region Fields

    private DispatcherTimer? _timer;
    private string? _targetProcessName;
    private HashSet<string>? _processWhitelist;
    private bool _lastCursorVisible = true;
    private bool _isRunning;
    private bool _isSuspended;
    private readonly Core.Interfaces.ILogService? _logService;
    private bool _enableDebugLog;

#endregion

#region Properties

    /// <summary>
    /// 是否正在运行检测
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 当前鼠标是否可见
    /// </summary>
    public bool IsCursorCurrentlyVisible => _lastCursorVisible;

    /// <summary>
    /// 目标进程名
    /// </summary>
    public string? TargetProcessName => _targetProcessName;

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    public bool EnableDebugLog
    {
        get => _enableDebugLog;
        set => _enableDebugLog = value;
    }

    /// <summary>
    /// 是否处于暂停状态
    /// </summary>
    public bool IsSuspended => _isSuspended;

#endregion

#region Constructor

    /// <summary>
    /// DI容器使用的构造函数
    /// </summary>
    /// <param name="logService">日志服务（可选）</param>
    public CursorDetectionService(Core.Interfaces.ILogService? logService = null)
    {
        _logService = logService;
    }

#endregion

#region Public Methods

    /// <summary>
    /// 启动鼠标检测
    /// </summary>
    /// <param name="targetProcessName">目标进程名（不含扩展名），仅当此进程在前台时检测</param>
    /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
    /// <param name="enableDebugLog">是否启用调试日志</param>
    public void Start(string? targetProcessName = null, int intervalMs = 200, bool enableDebugLog = false)
    {
        if (_isRunning)
        {
            Stop();
        }

        _targetProcessName = targetProcessName;
        _processWhitelist = null;
        _enableDebugLog = enableDebugLog;
        _lastCursorVisible = true; // 重置状态

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, intervalMs)) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
        _isRunning = true;

        LogDebug(
            nameof(CursorDetectionService),
            "CursorDetectionService started: TargetProcess={TargetProcess}, Interval={Interval}ms, DebugLog={DebugLog}",
            targetProcessName ?? "null", intervalMs, enableDebugLog);
    }

    /// <summary>
    /// 启动鼠标检测（使用白名单）
    /// </summary>
    /// <param name="whitelist">进程白名单，仅当这些进程在前台时检测</param>
    /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
    /// <param name="enableDebugLog">是否启用调试日志</param>
    public void StartWithWhitelist(HashSet<string> whitelist, int intervalMs = 200, bool enableDebugLog = false)
    {
        if (_isRunning)
        {
            Stop();
        }

        _targetProcessName = null;
        _processWhitelist = whitelist;
        _enableDebugLog = enableDebugLog;
        _lastCursorVisible = true;          // 重置状态
        _lastForegroundInWhitelist = false; // 重置前台进程状态

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, intervalMs)) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
        _isRunning = true;

        LogDebug(
            nameof(CursorDetectionService),
            "CursorDetectionService started with whitelist: Count={Count}, Interval={Interval}ms, DebugLog={DebugLog}",
            whitelist.Count, intervalMs, enableDebugLog);

        // 启动后立即检测一次当前状态
        CheckInitialState();
    }

    /// <summary>
    /// 停止鼠标检测
    /// </summary>
    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer = null;
        }
        _isRunning = false;
        _isSuspended = false;
        _targetProcessName = null;
        _processWhitelist = null;

        LogDebug(nameof(CursorDetectionService), "CursorDetectionService stopped");
    }

    /// <summary>
    /// 暂停鼠标检测（全屏时使用）
    /// </summary>
    public void Suspend()
    {
        if (!_isRunning || _isSuspended)
            return;

        _isSuspended = true;

        // 暂停定时器
        _timer?.Stop();

        LogDebug(nameof(CursorDetectionService), "CursorDetectionService suspended");
    }

    /// <summary>
    /// 恢复鼠标检测（退出全屏时使用）
    /// </summary>
    public void Resume()
    {
        if (!_isRunning || !_isSuspended)
            return;

        _isSuspended = false;

        // 恢复定时器
        _timer?.Start();

        LogDebug(nameof(CursorDetectionService), "CursorDetectionService resumed");

        // 恢复后立即检测当前状态
        CheckInitialState();
    }

    /// <summary>
    /// 更新目标进程名
    /// </summary>
    /// <param name="processName">新的目标进程名</param>
    public void SetTargetProcess(string? processName)
    {
        _targetProcessName = processName;
        LogDebug(nameof(CursorDetectionService), "TargetProcess updated to {ProcessName}", processName ?? "null");
    }

    /// <summary>
    /// 更新检测间隔
    /// </summary>
    /// <param name="intervalMs">新的检测间隔（毫秒）</param>
    public void SetInterval(int intervalMs)
    {
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, intervalMs));
            LogDebug(nameof(CursorDetectionService), "CheckInterval updated to {Interval}ms", intervalMs);
        }
    }

#endregion

#region Private Methods

    /// <summary>
    /// 上一次检测时前台进程是否在白名单中
    /// </summary>
    private bool _lastForegroundInWhitelist;

    /// <summary>
    /// 定时器回调：检测鼠标状态
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        // 暂停状态下不检测
        if (_isSuspended)
            return;

        // 检查前台进程是否匹配
        bool foregroundInWhitelist = CheckForegroundProcess();

        // 如果前台进程从白名单切换到非白名单，触发 CursorHidden 恢复透明度
        if (!foregroundInWhitelist && _lastForegroundInWhitelist)
        {
            _lastForegroundInWhitelist = false;

            // 只有之前是显示状态才需要恢复
            if (_lastCursorVisible)
            {
                _lastCursorVisible = false;
                LogDebug(nameof(CursorDetectionService),
                         "Foreground changed to non-whitelist process, restoring opacity");
                CursorHidden?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        _lastForegroundInWhitelist = foregroundInWhitelist;

        if (!foregroundInWhitelist)
        {
            return;
        }

        // 使用 ClipCursor 检测光标状态
        // 光标被限制 = 游戏模式（鼠标隐藏）
        // 光标自由 = UI 模式（鼠标可见）
        bool isClipped = Win32Helper.IsCursorClippedToCenter();
        bool cursorVisible = !isClipped;

        // 如果按住 Alt 键，视为鼠标隐藏状态（Alt 呼出的鼠标是临时的，不需要降低透明度）
        if (cursorVisible && Win32Helper.IsKeyPressed(Win32Helper.VK_MENU))
        {
            cursorVisible = false;
            LogDebug(nameof(CursorDetectionService), "Alt key pressed, treating as cursor hidden");
        }

        LogDebug(nameof(CursorDetectionService),
                 "Cursor detection: IsClipped={IsClipped}, CursorVisible={CursorVisible}", isClipped, cursorVisible);

        // 状态变化时触发事件
        if (cursorVisible != _lastCursorVisible)
        {
            _lastCursorVisible = cursorVisible;

            LogDebug(nameof(CursorDetectionService), "Cursor state changed: {State}",
                     cursorVisible ? "VISIBLE" : "HIDDEN");

            if (cursorVisible)
            {
                CursorShown?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                CursorHidden?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// 检查前台进程是否匹配目标进程或白名单
    /// </summary>
    private bool CheckForegroundProcess()
    {
        // 如果没有指定任何进程限制，始终检测
        if (string.IsNullOrEmpty(_targetProcessName) && _processWhitelist == null)
        {
            return true;
        }

        var foregroundProcess = Win32Helper.GetForegroundWindowProcessName();
        if (string.IsNullOrEmpty(foregroundProcess))
        {
            return false;
        }

        // 检查单个目标进程
        if (!string.IsNullOrEmpty(_targetProcessName))
        {
            return string.Equals(foregroundProcess, _targetProcessName, StringComparison.OrdinalIgnoreCase);
        }

        // 检查白名单
        if (_processWhitelist != null && _processWhitelist.Count > 0)
        {
            return _processWhitelist.Contains(foregroundProcess);
        }

        return false;
    }

    /// <summary>
    /// 启动时检测当前状态，如果游戏已在前台且鼠标可见，立即触发 CursorShown
    /// </summary>
    private void CheckInitialState()
    {
        // 检查前台进程是否匹配
        bool foregroundInWhitelist = CheckForegroundProcess();
        _lastForegroundInWhitelist = foregroundInWhitelist;

        // 使用 Info 级别日志，确保能看到初始检测结果
        Services.LogService.Instance.Info(nameof(CursorDetectionService),
                                          "Initial check: foregroundInWhitelist={InWhitelist}", foregroundInWhitelist);

        if (!foregroundInWhitelist)
        {
            return;
        }

        // 检测当前鼠标状态
        bool isClipped = Win32Helper.IsCursorClippedToCenter();
        bool cursorVisible = !isClipped;

        // Alt 键检测
        if (cursorVisible && Win32Helper.IsKeyPressed(Win32Helper.VK_MENU))
        {
            cursorVisible = false;
        }

        Services.LogService.Instance.Info(nameof(CursorDetectionService),
                                          "Initial check: IsClipped={IsClipped}, CursorVisible={CursorVisible}",
                                          isClipped, cursorVisible);

        // 更新状态并触发事件
        _lastCursorVisible = cursorVisible;

        if (cursorVisible)
        {
            Services.LogService.Instance.Info(nameof(CursorDetectionService),
                                              "Initial state: cursor VISIBLE, triggering CursorShown");
            CursorShown?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Services.LogService.Instance.Info(nameof(CursorDetectionService), "Initial state: cursor HIDDEN");
        }
    }

    /// <summary>
    /// 输出调试日志（仅在启用时）
    /// </summary>
    private void LogDebug(string source, string message, params object[] args)
    {
        if (_enableDebugLog && _logService != null)
        {
            _logService.Debug(source, message, args);
        }
    }

#endregion

#region IDisposable

    private bool _disposed;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Stop();
            }
            _disposed = true;
        }
    }

#endregion
}
}
