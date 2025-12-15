using System;
using System.Collections.Generic;
using System.Diagnostics;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 语音识别 API
    /// 提供插件监听语音识别结果的功能
    /// 需要 "audio" 权限
    /// </summary>
    public class SpeechApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly List<KeywordListener> _keywordListeners = new();
        private readonly List<Action<string>> _textListeners = new();
        private readonly object _lock = new();
        private bool _isSubscribed;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建语音 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public SpeechApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 检查语音识别是否可用
        /// </summary>
        /// <returns>语音识别是否可用</returns>
        public bool IsAvailable()
        {
            try
            {
                return SpeechService.Instance.IsModelInstalled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 监听特定关键词
        /// 当识别结果包含任一关键词时触发回调
        /// </summary>
        /// <param name="keywords">要监听的关键词列表</param>
        /// <param name="callback">回调函数，参数：(matchedKeyword, fullText)</param>
        public void OnKeyword(string[] keywords, Action<string, string> callback)
        {
            if (keywords == null || keywords.Length == 0 || callback == null)
                return;

            lock (_lock)
            {
                _keywordListeners.Add(new KeywordListener(keywords, callback));
                EnsureSubscribed();
            }

            Log($"注册关键词监听: [{string.Join(", ", keywords)}]");
        }

        /// <summary>
        /// 监听所有识别文本
        /// </summary>
        /// <param name="callback">回调函数，参数：(text)</param>
        public void OnText(Action<string> callback)
        {
            if (callback == null)
                return;

            lock (_lock)
            {
                _textListeners.Add(callback);
                EnsureSubscribed();
            }

            Log("注册文本监听");
        }

        /// <summary>
        /// 移除当前插件注册的所有监听器
        /// </summary>
        public void RemoveAllListeners()
        {
            lock (_lock)
            {
                _keywordListeners.Clear();
                _textListeners.Clear();
                
                // 如果没有监听器了，取消订阅事件
                if (_keywordListeners.Count == 0 && _textListeners.Count == 0)
                {
                    Unsubscribe();
                }
            }

            Log("已移除所有监听器");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            RemoveAllListeners();
        }

        /// <summary>
        /// 处理语音识别结果（用于测试）
        /// </summary>
        internal void ProcessSpeechResult(string text)
        {
            OnTextRecognized(text);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 确保已订阅语音识别事件
        /// </summary>
        private void EnsureSubscribed()
        {
            if (_isSubscribed)
                return;

            try
            {
                SpeechService.Instance.TextRecognized += OnSpeechServiceTextRecognized;
                _isSubscribed = true;
            }
            catch (Exception ex)
            {
                Log($"订阅语音识别事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅语音识别事件
        /// </summary>
        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            try
            {
                SpeechService.Instance.TextRecognized -= OnSpeechServiceTextRecognized;
                _isSubscribed = false;
            }
            catch (Exception ex)
            {
                Log($"取消订阅语音识别事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 语音识别事件处理
        /// </summary>
        private void OnSpeechServiceTextRecognized(object? sender, SpeechResult e)
        {
            if (string.IsNullOrWhiteSpace(e.Text))
                return;

            OnTextRecognized(e.Text);
        }

        /// <summary>
        /// 处理识别到的文本
        /// </summary>
        private void OnTextRecognized(string text)
        {
            // 忽略空白文本
            if (string.IsNullOrWhiteSpace(text))
                return;

            List<KeywordListener> keywordListenersCopy;
            List<Action<string>> textListenersCopy;

            lock (_lock)
            {
                keywordListenersCopy = new List<KeywordListener>(_keywordListeners);
                textListenersCopy = new List<Action<string>>(_textListeners);
            }

            // 调用文本监听器
            foreach (var listener in textListenersCopy)
            {
                try
                {
                    listener(text);
                }
                catch (Exception ex)
                {
                    Log($"文本监听器回调异常: {ex.Message}");
                }
            }

            // 调用关键词监听器
            foreach (var listener in keywordListenersCopy)
            {
                var matchedKeyword = listener.FindMatchingKeyword(text);
                if (matchedKeyword != null)
                {
                    try
                    {
                        listener.Callback(matchedKeyword, text);
                    }
                    catch (Exception ex)
                    {
                        Log($"关键词监听器回调异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            Debug.WriteLine($"[SpeechApi:{_context.PluginId}] {message}");
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// 关键词监听器
        /// </summary>
        private class KeywordListener
        {
            public string[] Keywords { get; }
            public Action<string, string> Callback { get; }

            public KeywordListener(string[] keywords, Action<string, string> callback)
            {
                Keywords = keywords;
                Callback = callback;
            }

            /// <summary>
            /// 查找匹配的关键词（不区分大小写）
            /// </summary>
            /// <param name="text">要搜索的文本</param>
            /// <returns>匹配的关键词，未找到返回 null</returns>
            public string? FindMatchingKeyword(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;

                foreach (var keyword in Keywords)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    // 不区分大小写的包含检查
                    if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        return keyword;
                    }
                }

                return null;
            }
        }

        #endregion
    }
}
