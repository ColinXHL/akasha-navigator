using System.Collections.Generic;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Window API
/// </summary>
public class WindowApi
{
    private readonly PluginContext _context;
    private readonly Func<Views.Windows.PlayerWindow?>? _getPlayerWindow;
    private EventManager? _eventManager;
    private ICursorDetectionService? _cursorDetectionService;
    private bool _isCursorDetectionStartedByThisApi;

    public WindowApi(PluginContext context, Func<Views.Windows.PlayerWindow?>? getPlayerWindow)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _getPlayerWindow = getPlayerWindow;
        _cursorDetectionService = CursorDetectionService.Instance;
    }

    public void SetEventManager(EventManager eventManager)
    {
        _eventManager = eventManager;
    }

    /// <summary>
    /// 设置 CursorDetectionService（用于测试注入）
    /// </summary>
    internal void SetCursorDetectionService(ICursorDetectionService service)
    {
        _cursorDetectionService = service;
    }

    [ScriptMember("getOpacity")]
    public double GetOpacity() => _getPlayerWindow?.Invoke()?.Opacity ?? 1.0;

    [ScriptMember("isClickThrough")]
    public bool IsClickThrough() => _getPlayerWindow?.Invoke()?.IsClickThrough ?? false;

    [ScriptMember("isTopmost")]
    public bool IsTopmost() => _getPlayerWindow?.Invoke()?.Topmost ?? true;

    [ScriptMember("getBounds")]
    public object GetBounds()
    {
        var window = _getPlayerWindow?.Invoke();
        if (window == null)
            return new { x = 0.0, y = 0.0, width = 0.0, height = 0.0 };
        return new { x = (double)window.Left, y = (double)window.Top, width = (double)window.Width,
                     height = (double)window.Height };
    }

    [ScriptMember("setOpacity")]
    public void SetOpacity(double opacity)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                                  {
                                                                      // 全屏时不允许插件修改透明度
                                                                      if (window.IsMaximized)
                                                                          return;

                                                                      // 使用 Win32 API 设置透明度，与之前的 C# 实现一致
                                                                      Helpers.Win32Helper.SetWindowOpacity(window,
                                                                                                           opacity);
                                                                  });
        }
    }

    [ScriptMember("setClickThrough")]
    public void SetClickThrough(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null && window.IsClickThrough != enabled)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.ToggleClickThrough());
    }

    [ScriptMember("setTopmost")]
    public void SetTopmost(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Topmost = enabled);
    }

    [ScriptMember("on")]
    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"window.{eventName}", callback) ?? -1;
    }

    [ScriptMember("off")]
    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"window.{eventName}");
    }

#region Cursor Detection API

    /// <summary>
    /// 启动鼠标检测
    /// </summary>
    /// <param name="options">选项对象，包含 processWhitelist 和 intervalMs</param>
    /// <returns>是否成功启动</returns>
    [ScriptMember("startCursorDetection")]
    public bool StartCursorDetection(object options)
    {
        if (_cursorDetectionService == null)
            return false;

        // 解析 options 参数
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int intervalMs = 200;

        try
        {
            // 使用 dynamic 访问 JavaScript 对象属性
            dynamic dynamicOptions = options;

            // 解析 processWhitelist
            try
            {
                var processList = dynamicOptions.processWhitelist;
                if (processList != null)
                {
                    // 尝试作为 JavaScript 数组处理
                    try
                    {
                        var length = (int)processList.length;
                        for (int i = 0; i < length; i++)
                        {
                            var processName = processList[i]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(processName))
                                whitelist.Add(processName);
                        }
                    }
                    catch
                    {
                        // 如果不是数组，尝试作为 IEnumerable 处理
                        if (processList is IEnumerable<object> enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                var processName = item?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(processName))
                                    whitelist.Add(processName);
                            }
                        }
                        else if (processList is IEnumerable<string> stringEnumerable)
                        {
                            foreach (var item in stringEnumerable)
                            {
                                var processName = item?.Trim();
                                if (!string.IsNullOrEmpty(processName))
                                    whitelist.Add(processName);
                            }
                        }
                    }
                }
            }
            catch
            {
                // processWhitelist 属性不存在
            }

            // 解析 intervalMs
            try
            {
                var intervalValue = dynamicOptions.intervalMs;
                if (intervalValue != null)
                {
                    intervalMs = Convert.ToInt32(intervalValue);
                }
            }
            catch
            {
                // intervalMs 属性不存在，使用默认值
            }
        }
        catch
        {
            // 解析失败，返回 false
            return false;
        }

        // 验证进程白名单非空
        if (whitelist.Count == 0)
        {
            Services.LogService.Instance.Warn(nameof(WindowApi),
                                              "startCursorDetection called with empty process whitelist");
            return false;
        }

        // 如果已经启动，先停止
        if (_isCursorDetectionStartedByThisApi && _cursorDetectionService.IsRunning)
        {
            StopCursorDetection();
        }

        // 订阅事件
        _cursorDetectionService.CursorShown += OnCursorShown;
        _cursorDetectionService.CursorHidden += OnCursorHidden;

        // 启动检测
        _cursorDetectionService.StartWithWhitelist(whitelist, intervalMs);
        _isCursorDetectionStartedByThisApi = true;

        Services.LogService.Instance.Info(nameof(WindowApi),
                                          "Cursor detection started: whitelist=[{Whitelist}], interval={Interval}ms",
                                          string.Join(", ", whitelist), intervalMs);

        return true;
    }

    /// <summary>
    /// 停止鼠标检测
    /// </summary>
    [ScriptMember("stopCursorDetection")]
    public void StopCursorDetection()
    {
        if (_cursorDetectionService == null)
            return;

        // 取消订阅事件
        _cursorDetectionService.CursorShown -= OnCursorShown;
        _cursorDetectionService.CursorHidden -= OnCursorHidden;

        // 停止检测
        _cursorDetectionService.Stop();
        _isCursorDetectionStartedByThisApi = false;

        Services.LogService.Instance.Info(nameof(WindowApi), "Cursor detection stopped");
    }

    /// <summary>
    /// 设置自动点击穿透状态
    /// </summary>
    /// <param name="enabled">是否启用</param>
    [ScriptMember("setAutoClickThrough")]
    public void SetAutoClickThrough(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                                  {
                                                                      // 全屏时不允许插件修改穿透状态
                                                                      if (window.IsMaximized)
                                                                          return;

                                                                      window.SetAutoClickThrough(enabled);
                                                                  });
        }
    }

    /// <summary>
    /// 获取自动点击穿透状态
    /// </summary>
    /// <returns>是否处于自动穿透模式</returns>
    [ScriptMember("isAutoClickThrough")]
    public bool IsAutoClickThrough()
    {
        var window = _getPlayerWindow?.Invoke();
        return window?.IsAutoClickThrough ?? false;
    }

    /// <summary>
    /// 鼠标显示事件处理
    /// </summary>
    private void OnCursorShown(object? sender, EventArgs e)
    {
        // 通过 EventManager 转发到插件
        _eventManager?.Emit("window.cursorShown");
    }

    /// <summary>
    /// 鼠标隐藏事件处理
    /// </summary>
    private void OnCursorHidden(object? sender, EventArgs e)
    {
        // 通过 EventManager 转发到插件
        _eventManager?.Emit("window.cursorHidden");
    }

#endregion
}
}
