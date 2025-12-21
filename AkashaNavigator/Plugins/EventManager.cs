using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 统一的事件管理器
/// 提供事件注册、移除和触发功能
/// 所有 API 共享此管理器实例
/// </summary>
public class EventManager
{
#region Fields

    private readonly object _lock = new object();
    private int _nextSubscriptionId = 1;

    // 存储事件监听器：eventName -> (subscriptionId -> callback)
    private readonly Dictionary<string, Dictionary<int, dynamic>> _listeners;

    // 存储订阅 ID 到事件名的映射，用于快速查找
    private readonly Dictionary<int, string> _subscriptionToEvent;

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
    /// <summary>配置变化事件</summary>
    public const string ConfigChanged = "configChanged";

    /// <summary>
    /// 所有支持的事件名称
    /// </summary>
    public static readonly string[] SupportedEvents =
        new[] { PlayStateChanged, TimeUpdate,     OpacityChanged, ClickThroughChanged,
                UrlChanged,       ProfileChanged, ConfigChanged };

#endregion

#region Constructor

    /// <summary>
    /// 创建事件管理器实例
    /// </summary>
    public EventManager()
    {
        _listeners = new Dictionary<string, Dictionary<int, dynamic>>(StringComparer.OrdinalIgnoreCase);
        _subscriptionToEvent = new Dictionary<int, string>();
    }

#endregion

#region Public Methods

    /// <summary>
    /// 注册事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数（支持 C# Action 或 JavaScript 函数）</param>
    /// <returns>订阅 ID，用于后续移除监听器</returns>
    [ScriptMember("on")]
    public int On(string eventName, dynamic callback)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            Services.LogService.Instance.Warn("EventManager", "On() called with empty event name");
            return -1;
        }

        if (callback == null)
        {
            Services.LogService.Instance.Warn("EventManager", "On() called with null callback for event '{EventName}'",
                                              eventName);
            return -1;
        }

        lock (_lock)
        {
            // 获取或创建该事件的监听器字典
            if (!_listeners.TryGetValue(eventName, out var eventListeners))
            {
                eventListeners = new Dictionary<int, dynamic>();
                _listeners[eventName] = eventListeners;
            }

            // 生成唯一的订阅 ID
            var subscriptionId = _nextSubscriptionId++;

            // 存储回调
            eventListeners[subscriptionId] = callback;
            _subscriptionToEvent[subscriptionId] = eventName;

            Services.LogService.Instance.Debug(
                "EventManager", "Registered listener for '{EventName}' with subscription ID {SubscriptionId}",
                eventName, subscriptionId);

            return subscriptionId;
        }
    }

    /// <summary>
    /// 移除指定订阅 ID 的事件监听器
    /// </summary>
    /// <param name="subscriptionId">订阅 ID（由 On() 返回）</param>
    /// <returns>是否成功移除</returns>
    [ScriptMember("off")]
    public bool Off(int subscriptionId)
    {
        if (subscriptionId < 0)
        {
            return false;
        }

        lock (_lock)
        {
            // 查找订阅 ID 对应的事件名
            if (!_subscriptionToEvent.TryGetValue(subscriptionId, out var eventName))
            {
                Services.LogService.Instance.Debug(
                    "EventManager", "Off() called with unknown subscription ID {SubscriptionId}", subscriptionId);
                return false;
            }

            // 从事件监听器中移除
            if (_listeners.TryGetValue(eventName, out var eventListeners))
            {
                if (eventListeners.Remove(subscriptionId))
                {
                    _subscriptionToEvent.Remove(subscriptionId);
                    Services.LogService.Instance.Debug(
                        "EventManager",
                        "Removed listener with subscription ID {SubscriptionId} from event '{EventName}'",
                        subscriptionId, eventName);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 移除指定事件的所有监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    [ScriptMember("offAll")]
    public void Off(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        lock (_lock)
        {
            if (_listeners.TryGetValue(eventName, out var eventListeners))
            {
                // 移除所有订阅 ID 的映射
                foreach (var subscriptionId in eventListeners.Keys)
                {
                    _subscriptionToEvent.Remove(subscriptionId);
                }

                // 清空该事件的所有监听器
                eventListeners.Clear();

                Services.LogService.Instance.Debug("EventManager", "Removed all listeners for event '{EventName}'",
                                                   eventName);
            }
        }
    }

    /// <summary>
    /// 触发事件，调用所有注册的回调
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">事件数据（可选）</param>
    [ScriptMember("emit")]
    public void Emit(string eventName, object? data = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        // 获取回调列表的快照，避免在遍历时修改
        KeyValuePair<int, dynamic>[] callbacksSnapshot;

        lock (_lock)
        {
            if (!_listeners.TryGetValue(eventName, out var eventListeners) || eventListeners.Count == 0)
            {
                return;
            }

            // 创建快照
            callbacksSnapshot = new KeyValuePair<int, dynamic>[eventListeners.Count];
            var index = 0;
            foreach (var kvp in eventListeners)
            {
                callbacksSnapshot[index++] = kvp;
            }
        }

        // 在锁外调用回调，避免死锁
        var invokedCount = 0;
        foreach (var kvp in callbacksSnapshot)
        {
            try
            {
                var callback = kvp.Value;

                // 调用回调，传递事件数据
                if (data != null)
                {
                    callback(data);
                }
                else
                {
                    callback();
                }

                invokedCount++;
            }
            catch (Exception ex)
            {
                // 记录错误但继续调用其他回调
                Services.LogService.Instance.Error("EventManager",
                                                   "Callback for event '{EventName}' (subscription ID " +
                                                       "{SubscriptionId}) threw exception: {ErrorMessage}",
                                                   eventName, kvp.Key, ex.Message);
            }
        }

        if (invokedCount > 0)
        {
            Services.LogService.Instance.Debug(
                "EventManager", "Emitted event '{EventName}' to {InvokedCount} listeners", eventName, invokedCount);
        }
    }

#endregion

#region Internal Methods

    /// <summary>
    /// 清除所有监听器
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _listeners.Clear();
            _subscriptionToEvent.Clear();
            Services.LogService.Instance.Debug("EventManager", "Cleared all listeners");
        }
    }

    /// <summary>
    /// 获取指定事件的监听器数量
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <returns>监听器数量</returns>
    public int GetListenerCount(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return 0;
        }

        lock (_lock)
        {
            if (_listeners.TryGetValue(eventName, out var eventListeners))
            {
                return eventListeners.Count;
            }
            return 0;
        }
    }

    /// <summary>
    /// 获取所有事件的总监听器数量
    /// </summary>
    /// <returns>总监听器数量</returns>
    public int GetTotalListenerCount()
    {
        lock (_lock)
        {
            var total = 0;
            foreach (var eventListeners in _listeners.Values)
            {
                total += eventListeners.Count;
            }
            return total;
        }
    }

    /// <summary>
    /// 检查指定订阅 ID 是否有效
    /// </summary>
    /// <param name="subscriptionId">订阅 ID</param>
    /// <returns>是否有效</returns>
    public bool HasSubscription(int subscriptionId)
    {
        lock (_lock)
        {
            return _subscriptionToEvent.ContainsKey(subscriptionId);
        }
    }

#endregion
}
}
