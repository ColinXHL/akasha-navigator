using System;
using System.Collections.Generic;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 事件系统 API
/// 提供应用程序事件监听功能
/// 需要 "events" 权限
///
/// 内部委托给共享的 EventManager 实现
/// </summary>
public class EventApi
{
#region Fields

    private readonly PluginContext _context;
    private EventManager? _eventManager;

    // 用于跟踪此 API 实例注册的订阅 ID，以便在 Cleanup 时移除
    private readonly List<int> _ownedSubscriptionIds = new List<int>();
    private readonly object _lock = new object();

#endregion

#region Event Names

    /// <summary>播放状态变化事件</summary>
    public const string PlayStateChanged = "playStateChanged";
    /// <summary>播放时间更新事件</summary>
    public const string TimeUpdate = "timeUpdate";
    /// <summary>透明度变化事件</summary>
    public const string OpacityChanged = "opacityChanged";
    /// <summary>穿透模式变化事件</summary>
    public const string ClickThroughChanged = "clickThroughChanged";
    /// <summary>URL 变化事件</summary>
    public const string UrlChanged = "urlChanged";
    /// <summary>Profile 切换事件</summary>
    public const string ProfileChanged = "profileChanged";

    /// <summary>
    /// 所有支持的事件名称
    /// </summary>
    public static readonly string[] SupportedEvents =
        new[] { PlayStateChanged, TimeUpdate, OpacityChanged, ClickThroughChanged, UrlChanged, ProfileChanged };

#endregion

#region Constructor

    /// <summary>
    /// 创建事件 API 实例
    /// </summary>
    /// <param name="context">插件上下文</param>
    public EventApi(PluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

#endregion

#region EventManager Integration

    /// <summary>
    /// 设置共享的 EventManager 实例
    /// 由 PluginApi 在初始化时调用
    /// </summary>
    /// <param name="eventManager">共享的事件管理器</param>
    internal void SetEventManager(EventManager eventManager)
    {
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
        Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "EventApi: EventManager set");
    }

    /// <summary>
    /// 获取内部使用的 EventManager
    /// 如果未设置，则创建一个独立的实例（用于测试或独立使用）
    /// </summary>
    private EventManager GetEventManager()
    {
        if (_eventManager == null)
        {
            lock (_lock)
            {
                if (_eventManager == null)
                {
                    _eventManager = new EventManager();
                    Services.LogService.Instance.Debug(
                        $"Plugin:{_context.PluginId}",
                        "EventApi: Created standalone EventManager (no shared instance provided)");
                }
            }
        }
        return _eventManager;
    }

#endregion

#region Public Methods

    /// <summary>
    /// 注册事件监听器（C# 版本，用于内部调用和测试）
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数</param>
    public void On(string eventName, Action<object> callback)
    {
        if (string.IsNullOrWhiteSpace(eventName) || callback == null)
            return;

        var subscriptionId = GetEventManager().On(eventName, callback);
        if (subscriptionId >= 0)
        {
            lock (_lock)
            {
                _ownedSubscriptionIds.Add(subscriptionId);
            }
            Services.LogService.Instance.Debug(
                $"Plugin:{_context.PluginId}",
                "EventApi: registered C# listener for '{EventName}' (subscription ID: {SubscriptionId})", eventName,
                subscriptionId);
        }
    }

    /// <summary>
    /// 注册事件监听器（V8 JavaScript 版本）
    /// 返回订阅 ID，可用于精确移除监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数（支持 V8 JavaScript 函数）</param>
    /// <returns>订阅 ID，失败返回 -1</returns>
    [ScriptMember("on")]
    public int OnJs(string eventName, object callback)
    {
        if (string.IsNullOrWhiteSpace(eventName) || callback == null)
            return -1;

        // 如果是 Action<object>，使用 C# 版本
        if (callback is Action<object> action)
        {
            On(eventName, action);
            // 返回最后添加的订阅 ID
            lock (_lock)
            {
                return _ownedSubscriptionIds.Count > 0 ? _ownedSubscriptionIds[_ownedSubscriptionIds.Count - 1] : -1;
            }
        }

        var subscriptionId = GetEventManager().On(eventName, callback);
        if (subscriptionId >= 0)
        {
            lock (_lock)
            {
                _ownedSubscriptionIds.Add(subscriptionId);
            }
            Services.LogService.Instance.Debug(
                $"Plugin:{_context.PluginId}",
                "EventApi: registered JS listener for '{EventName}' (subscription ID: {SubscriptionId})", eventName,
                subscriptionId);
        }
        return subscriptionId;
    }

    /// <summary>
    /// 取消事件监听（C# 版本）
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数（为 null 时移除该事件的所有监听器）</param>
    public void Off(string eventName, Action<object>? callback)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        if (callback == null)
        {
            // 移除该事件的所有监听器
            GetEventManager().Off(eventName);
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}",
                                               "EventApi: removed all listeners for '{EventName}'", eventName);
        }
        else
        {
            // 注意：EventManager 使用订阅 ID 而不是回调引用来移除
            // 这里为了向后兼容，我们移除该事件的所有监听器
            // 因为无法通过回调引用找到对应的订阅 ID
            GetEventManager().Off(eventName);
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}",
                                               "EventApi: removed listeners for '{EventName}' (callback-based removal)",
                                               eventName);
        }
    }

    /// <summary>
    /// 取消事件监听（V8 JavaScript 版本）
    /// 支持通过订阅 ID 或事件名称移除
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="subscriptionIdOrCallback">订阅 ID（int）或回调函数（为 null 时移除该事件的所有监听器）</param>
    [ScriptMember("off")]
    public void OffJs(string eventName, object? subscriptionIdOrCallback = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        // 如果传入的是订阅 ID（整数）
        if (subscriptionIdOrCallback is int subscriptionId)
        {
            if (GetEventManager().Off(subscriptionId))
            {
                lock (_lock)
                {
                    _ownedSubscriptionIds.Remove(subscriptionId);
                }
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}",
                                                   "EventApi: removed listener with subscription ID {SubscriptionId}",
                                                   subscriptionId);
            }
            return;
        }

        // 如果传入的是 double（JavaScript 数字类型）
        if (subscriptionIdOrCallback is double doubleId)
        {
            var intId = (int)doubleId;
            if (GetEventManager().Off(intId))
            {
                lock (_lock)
                {
                    _ownedSubscriptionIds.Remove(intId);
                }
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}",
                                                   "EventApi: removed listener with subscription ID {SubscriptionId}",
                                                   intId);
            }
            return;
        }

        // 如果是 Action<object>，使用 C# 版本
        if (subscriptionIdOrCallback is Action<object> action)
        {
            Off(eventName, action);
            return;
        }

        // 如果是 null 或其他类型，移除该事件的所有监听器
        GetEventManager().Off(eventName);
        Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}",
                                           "EventApi: removed all listeners for '{EventName}'", eventName);
    }

#endregion

#region Internal Methods

    /// <summary>
    /// 触发事件（供主程序调用）
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">事件数据</param>
    public void Emit(string eventName, object data)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        GetEventManager().Emit(eventName, data);
    }

    /// <summary>
    /// 清除所有监听器（插件卸载时调用）
    /// 只清除此 API 实例注册的监听器
    /// </summary>
    internal void ClearAllListeners()
    {
        lock (_lock)
        {
            // 移除此实例注册的所有订阅
            foreach (var subscriptionId in _ownedSubscriptionIds)
            {
                GetEventManager().Off(subscriptionId);
            }
            _ownedSubscriptionIds.Clear();
        }
        Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "EventApi: cleared all owned listeners");
    }

    /// <summary>
    /// 清理资源（插件卸载时调用）
    /// </summary>
    internal void Cleanup()
    {
        ClearAllListeners();
    }

    /// <summary>
    /// 获取指定事件的监听器数量
    /// </summary>
    internal int GetListenerCount(string eventName)
    {
        return GetEventManager().GetListenerCount(eventName);
    }

#endregion
}
}
