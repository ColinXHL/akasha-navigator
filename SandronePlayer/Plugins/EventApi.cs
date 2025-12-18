using System;
using System.Collections.Generic;
using Microsoft.ClearScript;

namespace SandronePlayer.Plugins
{
    /// <summary>
    /// 事件系统 API
    /// 提供应用程序事件监听功能
    /// 需要 "events" 权限
    /// </summary>
    public class EventApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly Dictionary<string, List<Action<object>>> _listeners;
        // V8 引擎的 JavaScript 回调函数列表
        private readonly Dictionary<string, List<dynamic>> _jsListeners;

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
        public static readonly string[] SupportedEvents = new[]
        {
            PlayStateChanged, TimeUpdate, OpacityChanged, 
            ClickThroughChanged, UrlChanged, ProfileChanged
        };

        #endregion

        #region Constructor

        /// <summary>
        /// 创建事件 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public EventApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _listeners = new Dictionary<string, List<Action<object>>>(StringComparer.OrdinalIgnoreCase);
            _jsListeners = new Dictionary<string, List<dynamic>>(StringComparer.OrdinalIgnoreCase);
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

            if (!_listeners.TryGetValue(eventName, out var list))
            {
                list = new List<Action<object>>();
                _listeners[eventName] = list;
            }

            if (!list.Contains(callback))
            {
                list.Add(callback);
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: registered C# listener for '{eventName}'");
            }
        }

        /// <summary>
        /// 注册事件监听器（V8 JavaScript 版本）
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">回调函数（支持 V8 JavaScript 函数）</param>
        [ScriptMember("on")]
        public void OnJs(string eventName, object callback)
        {
            if (string.IsNullOrWhiteSpace(eventName) || callback == null)
                return;

            // 如果是 Action<object>，使用 C# 版本
            if (callback is Action<object> action)
            {
                On(eventName, action);
                return;
            }

            if (!_jsListeners.TryGetValue(eventName, out var list))
            {
                list = new List<dynamic>();
                _jsListeners[eventName] = list;
            }

            list.Add(callback);
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: registered JS listener for '{eventName}'");
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

            if (!_listeners.TryGetValue(eventName, out var list))
                return;

            if (callback == null)
            {
                list.Clear();
                // 同时清理 JS 监听器
                if (_jsListeners.TryGetValue(eventName, out var jsList))
                {
                    jsList.Clear();
                }
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed all listeners for '{eventName}'");
            }
            else
            {
                list.Remove(callback);
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed C# listener for '{eventName}'");
            }
        }

        /// <summary>
        /// 取消事件监听（V8 JavaScript 版本）
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">回调函数（为 null 时移除该事件的所有监听器）</param>
        [ScriptMember("off")]
        public void OffJs(string eventName, object? callback = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            // 如果是 Action<object>，使用 C# 版本
            if (callback is Action<object> action)
            {
                Off(eventName, action);
                return;
            }

            // 清理 JavaScript 监听器
            if (_jsListeners.TryGetValue(eventName, out var jsList))
            {
                if (callback == null)
                {
                    jsList.Clear();
                    // 同时清理 C# 监听器
                    if (_listeners.TryGetValue(eventName, out var list))
                    {
                        list.Clear();
                    }
                    Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed all listeners for '{eventName}'");
                }
                else
                {
                    jsList.Remove(callback);
                    Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed JS listener for '{eventName}'");
                }
            }
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

            var hasListeners = false;
            var totalCount = 0;

            // 调用 C# 监听器
            if (_listeners.TryGetValue(eventName, out var list) && list.Count > 0)
            {
                hasListeners = true;
                totalCount += list.Count;
                var callbacks = list.ToArray();
                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback(data);
                    }
                    catch (Exception ex)
                    {
                        Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                            $"EventApi: C# callback for '{eventName}' threw exception: {ex.Message}");
                    }
                }
            }

            // 调用 JavaScript 监听器
            if (_jsListeners.TryGetValue(eventName, out var jsList) && jsList.Count > 0)
            {
                hasListeners = true;
                totalCount += jsList.Count;
                var jsCallbacks = jsList.ToArray();
                foreach (var jsCallback in jsCallbacks)
                {
                    try
                    {
                        jsCallback(data);
                    }
                    catch (Exception ex)
                    {
                        Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                            $"EventApi: JS callback for '{eventName}' threw exception: {ex.Message}");
                    }
                }
            }

            if (hasListeners)
            {
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: emitted '{eventName}' to {totalCount} listeners");
            }
        }

        /// <summary>
        /// 清除所有监听器（插件卸载时调用）
        /// </summary>
        internal void ClearAllListeners()
        {
            _listeners.Clear();
            _jsListeners.Clear();
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "EventApi: cleared all listeners");
        }

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            ClearAllListeners();
        }

        /// <summary>
        /// 获取指定事件的监听器数量（包括 C# 和 JS 监听器）
        /// </summary>
        internal int GetListenerCount(string eventName)
        {
            var count = 0;
            if (_listeners.TryGetValue(eventName, out var list))
                count += list.Count;
            if (_jsListeners.TryGetValue(eventName, out var jsList))
                count += jsList.Count;
            return count;
        }

        #endregion
    }
}
