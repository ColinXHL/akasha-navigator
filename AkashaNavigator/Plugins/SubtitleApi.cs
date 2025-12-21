using System;
using System.Collections.Generic;
using Microsoft.ClearScript;
using AkashaNavigator.Models.Data;
using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 字幕 API
/// 提供插件访问视频字幕数据的功能
/// 需要 "subtitle" 权限
/// </summary>
public class SubtitleApi
{
#region Constants

    /// <summary>字幕变化事件</summary>
    public const string EventChange = "change";
    /// <summary>字幕加载事件</summary>
    public const string EventLoad = "load";
    /// <summary>字幕清除事件</summary>
    public const string EventClear = "clear";

#endregion

#region Fields

    private readonly PluginContext _context;
    private EventManager _eventManager;
    private readonly object _lock = new();
    private bool _isSubscribed;

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
        // 创建独立的 EventManager，后续可通过 SetEventManager 替换为共享实例
        _eventManager = new EventManager();
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
        Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "SubtitleApi: EventManager set");
    }

#endregion

#region Properties

    /// <summary>
    /// 检查是否有字幕数据
    /// </summary>
    [ScriptMember("hasSubtitles")]
    public bool HasSubtitles
    {
        get {
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

#region Public Methods - Unified Event API

    /// <summary>
    /// 注册事件监听器（统一事件 API）
    /// </summary>
    /// <param name="eventName">事件名称：change, load, clear</param>
    /// <param name="callback">回调函数</param>
    /// <returns>订阅 ID，用于后续移除；无效参数返回 -1</returns>
    [ScriptMember("on")]
    public int On(string eventName, object callback)
    {
        if (string.IsNullOrWhiteSpace(eventName) || callback == null)
            return -1;

        // 规范化事件名称
        var normalizedName = eventName.ToLowerInvariant();

        // 验证事件名称
        if (normalizedName != EventChange && normalizedName != EventLoad && normalizedName != EventClear)
        {
            Log("未知的事件名称: {EventName}", eventName);
            return -1;
        }

        lock (_lock)
        {
            EnsureSubscribed();
        }

        var subscriptionId = _eventManager.On(normalizedName, callback);
        Log("注册事件监听 '{EventName}', ID: {SubscriptionId}", normalizedName, subscriptionId);

        // 如果是 load 事件且字幕已存在，立即触发回调
        if (normalizedName == EventLoad)
        {
            TriggerExistingSubtitleCallback(callback);
        }

        return subscriptionId;
    }

    /// <summary>
    /// 移除事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="subscriptionId">订阅 ID（可选，不提供则移除该事件所有监听器）</param>
    /// <returns>是否成功移除</returns>
    [ScriptMember("off")]
    public bool Off(string eventName, int subscriptionId = -1)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        var normalizedName = eventName.ToLowerInvariant();

        if (subscriptionId >= 0)
        {
            var result = _eventManager.Off(subscriptionId);
            if (result)
            {
                Log("移除事件监听 '{EventName}', ID: {SubscriptionId}", normalizedName, subscriptionId);
                CheckAndUnsubscribe();
            }
            return result;
        }
        else
        {
            _eventManager.Off(normalizedName);
            Log("移除所有 '{EventName}' 事件监听", normalizedName);
            CheckAndUnsubscribe();
            return true;
        }
    }

#endregion

#region Public Methods - Data Access

    /// <summary>
    /// 根据时间戳获取当前字幕
    /// </summary>
    /// <param name="timeInSeconds">时间戳（秒）</param>
    /// <returns>匹配的字幕条目，无匹配返回 null</returns>
    [ScriptMember("getCurrent")]
    public object? GetCurrent(double timeInSeconds)
    {
        try
        {
            var entry = SubtitleService.Instance.GetSubtitleAt(timeInSeconds);
            return ConvertSubtitleEntryToJs(entry);
        }
        catch (Exception ex)
        {
            Log("获取当前字幕失败: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 获取所有字幕
    /// </summary>
    /// <returns>字幕条目列表，无数据返回空数组</returns>
    [ScriptMember("getAll")]
    public object GetAll()
    {
        try
        {
            var entries = SubtitleService.Instance.GetAllSubtitles();
            return ConvertSubtitleListToJs(entries);
        }
        catch (Exception ex)
        {
            Log("获取所有字幕失败: {ErrorMessage}", ex.Message);
            return _context.CreateJsArray() ?? Array.Empty<object>();
        }
    }

#endregion

#region Internal Methods

    /// <summary>
    /// 清理资源（插件卸载时调用）
    /// </summary>
    internal void Cleanup()
    {
        _eventManager.Clear();
        Unsubscribe();
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
            Log("订阅字幕服务事件失败: {ErrorMessage}", ex.Message);
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
            Log("取消订阅字幕服务事件失败: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// 检查是否需要取消订阅
    /// </summary>
    private void CheckAndUnsubscribe()
    {
        lock (_lock)
        {
            if (_eventManager.GetListenerCount(EventChange) == 0 && _eventManager.GetListenerCount(EventLoad) == 0 &&
                _eventManager.GetListenerCount(EventClear) == 0)
            {
                Unsubscribe();
            }
        }
    }

    /// <summary>
    /// 如果字幕已存在，触发回调
    /// </summary>
    private void TriggerExistingSubtitleCallback(object callback)
    {
        try
        {
            var existingData = SubtitleService.Instance.GetSubtitleData();
            if (existingData != null && existingData.Body.Count > 0)
            {
                Log("字幕已存在，立即触发回调");
                var jsData = ConvertSubtitleDataToJs(existingData);
                if (jsData != null)
                {
                    ((dynamic)callback)(jsData);
                }
            }
        }
        catch (Exception ex)
        {
            Log("触发已有字幕回调失败: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// 字幕变化事件处理
    /// </summary>
    private void OnServiceSubtitleChanged(object? sender, SubtitleEntry? e)
    {
        OnSubtitleChanged?.Invoke(this, e);
        var jsEntry = ConvertSubtitleEntryToJs(e);
        _eventManager.Emit(EventChange, jsEntry);
    }

    /// <summary>
    /// 字幕加载事件处理
    /// </summary>
    private void OnServiceSubtitleLoaded(object? sender, SubtitleData e)
    {
        OnSubtitleLoaded?.Invoke(this, e);
        var jsData = ConvertSubtitleDataToJs(e);
        _eventManager.Emit(EventLoad, jsData);
    }

    /// <summary>
    /// 字幕清除事件处理
    /// </summary>
    private void OnServiceSubtitleCleared(object? sender, EventArgs e)
    {
        OnSubtitleCleared?.Invoke(this, EventArgs.Empty);
        _eventManager.Emit(EventClear);
    }

    /// <summary>
    /// 将 SubtitleEntry 转换为 JS 对象
    /// </summary>
    private object? ConvertSubtitleEntryToJs(SubtitleEntry? entry)
    {
        if (entry == null)
            return null;

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
    /// 将 SubtitleData 转换为 JS 对象
    /// </summary>
    private object? ConvertSubtitleDataToJs(SubtitleData? data)
    {
        if (data == null)
            return null;

        var jsBody = ConvertSubtitleListToJs(data.Body);

        dynamic? jsResult = _context.CreateJsObject();
        if (jsResult != null)
        {
            jsResult.language = data.Language;
            jsResult.body = jsBody;
            jsResult.sourceUrl = data.SourceUrl;
            return jsResult;
        }

        // 回退到 PropertyBag
        var result = new PropertyBag();
        result["language"] = data.Language;
        result["body"] = jsBody;
        result["sourceUrl"] = data.SourceUrl;
        return result;
    }

    /// <summary>
    /// 将字幕列表转换为 JS 数组
    /// </summary>
    private object ConvertSubtitleListToJs(IReadOnlyList<SubtitleEntry> entries)
    {
        dynamic? jsArray = _context.CreateJsArray();
        if (jsArray != null)
        {
            foreach (var entry in entries)
            {
                var jsEntry = ConvertSubtitleEntryToJs(entry);
                if (jsEntry != null)
                {
                    jsArray.push(jsEntry);
                }
            }
            return jsArray;
        }

        // 回退到普通数组
        var result = new object?[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            result[i] = ConvertSubtitleEntryToJs(entries[i]);
        }
        return result;
    }

    /// <summary>
    /// 记录日志（参数化模板）
    /// </summary>
    private void Log(string template, params object[] args)
    {
        LogService.Instance.Debug($"Plugin:{_context.PluginId}", template, args);
    }

#endregion
}
}
