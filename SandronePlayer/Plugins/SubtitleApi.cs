using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ClearScript;
using SandronePlayer.Models;
using SandronePlayer.Services;

namespace SandronePlayer.Plugins
{
    /// <summary>
    /// 字幕 API
    /// 提供插件访问视频字幕数据的功能
    /// 需要 "subtitle" 权限
    /// </summary>
    public class SubtitleApi
    {
        #region Fields

        private readonly PluginContext _context;
        // JavaScript 回调函数字典（使用 ID 作为键）
        private readonly Dictionary<int, dynamic> _jsSubtitleChangedListeners = new();
        private readonly Dictionary<int, dynamic> _jsSubtitleLoadedListeners = new();
        private readonly Dictionary<int, dynamic> _jsSubtitleClearedListeners = new();
        private readonly object _lock = new();
        private bool _isSubscribed;
        private int _nextListenerId = 0;

        #endregion

        #region Events

        /// <summary>
        /// 当前字幕变化事件
        /// </summary>
        public event EventHandler<SubtitleEntry?>? OnSubtitleChanged;

        /// <summary>
        /// 字幕数据加载完成事件
        /// </summary>
        public event EventHandler<SubtitleData>? OnSubtitleLoaded;

        /// <summary>
        /// 字幕数据清除事件
        /// </summary>
        public event EventHandler? OnSubtitleCleared;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建字幕 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public SubtitleApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion


        #region Properties

        /// <summary>
        /// 检查是否有字幕数据
        /// </summary>
        [ScriptMember("hasSubtitles")]
        public bool HasSubtitles
        {
            get
            {
                try
                {
                    var data = SubtitleService.Instance.GetSubtitleData();
                    return data != null && data.Body.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 根据时间戳获取当前字幕
        /// </summary>
        /// <param name="timeInSeconds">时间戳（秒）</param>
        /// <returns>匹配的字幕条目，无匹配返回 null</returns>
        [ScriptMember("getCurrent")]
        public SubtitleEntry? GetCurrent(double timeInSeconds)
        {
            try
            {
                return SubtitleService.Instance.GetSubtitleAt(timeInSeconds);
            }
            catch (Exception ex)
            {
                Log($"获取当前字幕失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有字幕
        /// </summary>
        /// <returns>字幕条目列表，无数据返回空列表</returns>
        [ScriptMember("getAll")]
        public IReadOnlyList<SubtitleEntry> GetAll()
        {
            try
            {
                return SubtitleService.Instance.GetAllSubtitles();
            }
            catch (Exception ex)
            {
                Log($"获取所有字幕失败: {ex.Message}");
                return Array.Empty<SubtitleEntry>();
            }
        }

        /// <summary>
        /// 监听字幕变化
        /// </summary>
        /// <param name="callback">回调函数，参数为当前字幕（可能为 null）</param>
        /// <returns>监听器 ID，用于后续移除；无效回调返回 -1</returns>
        [ScriptMember("onChanged")]
        public int OnChanged(object callback)
        {
            if (callback == null)
                return -1;

            int listenerId;
            lock (_lock)
            {
                listenerId = _nextListenerId++;
                _jsSubtitleChangedListeners[listenerId] = callback;
                EnsureSubscribed();
            }

            Log($"注册字幕变化监听 (V8), ID: {listenerId}");
            return listenerId;
        }

        /// <summary>
        /// 监听字幕加载
        /// </summary>
        /// <param name="callback">回调函数，参数为加载的字幕数据</param>
        /// <returns>监听器 ID，用于后续移除；无效回调返回 -1</returns>
        [ScriptMember("onLoaded")]
        public int OnLoaded(object callback)
        {
            if (callback == null)
                return -1;

            int listenerId;
            lock (_lock)
            {
                listenerId = _nextListenerId++;
                _jsSubtitleLoadedListeners[listenerId] = callback;
                EnsureSubscribed();
            }

            Log($"注册字幕加载监听 (V8), ID: {listenerId}");

            // 如果字幕已经加载，立即触发一次回调
            try
            {
                var existingData = SubtitleService.Instance.GetSubtitleData();
                if (existingData != null && existingData.Body.Count > 0)
                {
                    Log("字幕已存在，立即触发回调");
                    InvokeJsCallback(callback, existingData);
                }
            }
            catch (Exception ex)
            {
                Log($"触发已有字幕回调失败: {ex.Message}");
            }

            return listenerId;
        }

        /// <summary>
        /// 监听字幕清除
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <returns>监听器 ID，用于后续移除；无效回调返回 -1</returns>
        [ScriptMember("onCleared")]
        public int OnCleared(object callback)
        {
            if (callback == null)
                return -1;

            int listenerId;
            lock (_lock)
            {
                listenerId = _nextListenerId++;
                _jsSubtitleClearedListeners[listenerId] = callback;
                EnsureSubscribed();
            }

            Log($"注册字幕清除监听 (V8), ID: {listenerId}");
            return listenerId;
        }

        /// <summary>
        /// 移除当前插件注册的所有监听器
        /// </summary>
        [ScriptMember("removeAllListeners")]
        public void RemoveAllListeners()
        {
            lock (_lock)
            {
                _jsSubtitleChangedListeners.Clear();
                _jsSubtitleLoadedListeners.Clear();
                _jsSubtitleClearedListeners.Clear();

                // 如果没有监听器了，取消订阅事件
                if (!HasAnyListeners())
                {
                    Unsubscribe();
                }
            }

            Log("已移除所有监听器");
        }

        /// <summary>
        /// 移除指定 ID 的监听器
        /// </summary>
        /// <param name="listenerId">监听器 ID</param>
        /// <returns>是否成功移除</returns>
        [ScriptMember("removeListener")]
        public bool RemoveListener(int listenerId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _jsSubtitleChangedListeners.Remove(listenerId) ||
                          _jsSubtitleLoadedListeners.Remove(listenerId) ||
                          _jsSubtitleClearedListeners.Remove(listenerId);

                // 如果没有监听器了，取消订阅
                if (removed && !HasAnyListeners())
                {
                    Unsubscribe();
                }
            }

            if (removed)
            {
                Log($"已移除监听器 ID: {listenerId}");
            }
            else
            {
                Log($"未找到监听器 ID: {listenerId}");
            }

            return removed;
        }

        /// <summary>
        /// 检查是否有任何监听器
        /// </summary>
        /// <returns>是否有监听器</returns>
        private bool HasAnyListeners()
        {
            return _jsSubtitleChangedListeners.Count > 0 ||
                   _jsSubtitleLoadedListeners.Count > 0 ||
                   _jsSubtitleClearedListeners.Count > 0;
        }

        #endregion


        #region Internal Methods

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            RemoveAllListeners();
            
            // 触发 V8 垃圾回收释放字幕相关内存
            _context.CollectGarbage();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 确保已订阅字幕服务事件
        /// </summary>
        private void EnsureSubscribed()
        {
            if (_isSubscribed)
                return;

            try
            {
                SubtitleService.Instance.SubtitleChanged += OnServiceSubtitleChanged;
                SubtitleService.Instance.SubtitleLoaded += OnServiceSubtitleLoaded;
                SubtitleService.Instance.SubtitleCleared += OnServiceSubtitleCleared;
                _isSubscribed = true;
            }
            catch (Exception ex)
            {
                Log($"订阅字幕服务事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅字幕服务事件
        /// </summary>
        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            try
            {
                SubtitleService.Instance.SubtitleChanged -= OnServiceSubtitleChanged;
                SubtitleService.Instance.SubtitleLoaded -= OnServiceSubtitleLoaded;
                SubtitleService.Instance.SubtitleCleared -= OnServiceSubtitleCleared;
                _isSubscribed = false;
            }
            catch (Exception ex)
            {
                Log($"取消订阅字幕服务事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 字幕变化事件处理
        /// </summary>
        private void OnServiceSubtitleChanged(object? sender, SubtitleEntry? e)
        {
            // 触发公开事件
            OnSubtitleChanged?.Invoke(this, e);

            // 获取监听器快照，使用 Values.ToArray() 减少内存分配
            dynamic[] jsListenersCopy;
            lock (_lock)
            {
                jsListenersCopy = _jsSubtitleChangedListeners.Values.ToArray();
            }

            // 调用 JavaScript 监听器
            foreach (var jsListener in jsListenersCopy)
            {
                InvokeJsCallback(jsListener, e);
            }
        }

        /// <summary>
        /// 字幕加载事件处理
        /// </summary>
        private void OnServiceSubtitleLoaded(object? sender, SubtitleData e)
        {
            // 触发公开事件
            OnSubtitleLoaded?.Invoke(this, e);

            // 获取监听器快照，使用 Values.ToArray() 减少内存分配
            dynamic[] jsListenersCopy;
            lock (_lock)
            {
                jsListenersCopy = _jsSubtitleLoadedListeners.Values.ToArray();
            }

            // 调用 JavaScript 监听器
            foreach (var jsListener in jsListenersCopy)
            {
                InvokeJsCallback(jsListener, e);
            }
        }

        /// <summary>
        /// 字幕清除事件处理
        /// </summary>
        private void OnServiceSubtitleCleared(object? sender, EventArgs e)
        {
            // 触发公开事件
            OnSubtitleCleared?.Invoke(this, EventArgs.Empty);

            // 获取监听器快照，使用 Values.ToArray() 减少内存分配
            dynamic[] jsListenersCopy;
            lock (_lock)
            {
                jsListenersCopy = _jsSubtitleClearedListeners.Values.ToArray();
            }

            // 调用 JavaScript 监听器
            foreach (var jsListener in jsListenersCopy)
            {
                InvokeJsCallback(jsListener);
            }
        }

        /// <summary>
        /// 调用 JavaScript 回调函数
        /// </summary>
        /// <param name="callback">JavaScript 函数对象</param>
        /// <param name="args">参数</param>
        private void InvokeJsCallback(dynamic callback, params object?[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    callback();
                }
                else if (args.Length == 1)
                {
                    var arg = ConvertToJsCompatible(args[0]);
                    callback(arg);
                }
                else
                {
                    // 多参数情况
                    var convertedArgs = new object?[args.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        convertedArgs[i] = ConvertToJsCompatible(args[i]);
                    }
                    callback(convertedArgs);
                }
            }
            catch (Exception ex)
            {
                Log($"JavaScript 回调异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 C# 对象转换为 JavaScript 兼容格式
        /// 使用 V8 引擎创建真正的 JS 原生对象和数组
        /// </summary>
        private object? ConvertToJsCompatible(object? obj)
        {
            if (obj == null)
                return null;

            // SubtitleData 转换为 JS 原生对象
            if (obj is SubtitleData data)
            {
                // 使用 V8 引擎创建真正的 JS 数组
                dynamic? jsArray = _context.CreateJsArray();
                if (jsArray != null)
                {
                    for (int i = 0; i < data.Body.Count; i++)
                    {
                        var jsEntry = ConvertSubtitleEntryToJs(data.Body[i]);
                        if (jsEntry != null)
                        {
                            jsArray.push(jsEntry);
                        }
                    }
                }

                // 使用 V8 引擎创建真正的 JS 对象
                dynamic? jsResult = _context.CreateJsObject();
                if (jsResult != null)
                {
                    jsResult.language = data.Language;
                    jsResult.body = jsArray;
                    jsResult.sourceUrl = data.SourceUrl;
                    return jsResult;
                }

                // 回退到 PropertyBag
                var result = new PropertyBag();
                result["language"] = data.Language;
                result["body"] = jsArray;
                result["sourceUrl"] = data.SourceUrl;
                return result;
            }

            // SubtitleEntry 转换为 JS 原生对象
            if (obj is SubtitleEntry entry)
            {
                return ConvertSubtitleEntryToJs(entry);
            }

            return obj;
        }

        /// <summary>
        /// 将 SubtitleEntry 转换为 JS 原生对象
        /// </summary>
        private object? ConvertSubtitleEntryToJs(SubtitleEntry entry)
        {
            dynamic? jsObj = _context.CreateJsObject();
            if (jsObj != null)
            {
                jsObj.from = entry.From;
                jsObj.to = entry.To;
                jsObj.content = entry.Content;
                return jsObj;
            }

            // 回退到 PropertyBag
            var result = new PropertyBag();
            result["from"] = entry.From;
            result["to"] = entry.To;
            result["content"] = entry.Content;
            return result;
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            LogService.Instance.Debug($"SubtitleApi:{_context.PluginId}", message);
        }

        #endregion
    }
}
