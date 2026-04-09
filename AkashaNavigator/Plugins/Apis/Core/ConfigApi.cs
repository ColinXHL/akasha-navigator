using System;
using System.Collections.Generic;
using AkashaNavigator.Core.Interfaces;
using Microsoft.ClearScript;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Utils;

namespace AkashaNavigator.Plugins.Apis.Core
{
/// <summary>
/// 配置 API
/// 提供插件配置的读写功能
/// 支持点号分隔的路径（如 "overlay.x"）
/// 无需权限
/// </summary>
public class ConfigApi
{
#region Fields

    private readonly PluginConfig _config;
    private readonly EventManager? _eventManager;
    private readonly ILogService _logService;

#endregion

#region Constructor

    /// <summary>
    /// 创建配置 API 实例
    /// </summary>
    /// <param name="config">插件配置</param>
    public ConfigApi(PluginConfig config) : this(config, null, null)
    {
    }

    /// <summary>
    /// 创建配置 API 实例（带事件管理器）
    /// </summary>
    /// <param name="config">插件配置</param>
    /// <param name="eventManager">事件管理器（可选，用于发射配置变更事件）</param>
    /// <param name="logService">日志服务（可选）</param>
    public ConfigApi(PluginConfig config, EventManager? eventManager, ILogService? logService = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _eventManager = eventManager;
        _logService = logService ?? Services.LogService.Instance;
    }

#endregion

#region Methods

    /// <summary>
    /// 获取配置值
    /// 支持点号分隔的路径，如 "overlay.x"
    /// </summary>
    /// <param name="key">配置键（支持点号路径）</param>
    /// <param name="defaultValue">默认值（可选）</param>
    /// <returns>配置值或默认值</returns>
    [ScriptMember("get")]
    public object? Get(string key, object? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return defaultValue;

        try
        {
            // 使用 PluginConfig 的 GetRaw 方法获取原始值
            var value = _config.GetRaw(key);
            return value ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ConfigApi), "Get({Key}) failed: {ErrorMessage}", key, ex.Message);
            return defaultValue;
        }
    }

    /// <summary>
    /// 设置配置值
    /// 支持点号分隔的路径，如 "overlay.x"
    /// 设置后自动保存到文件
    /// 当值发生变化时，发射 "configChanged" 事件
    /// </summary>
    /// <param name="key">配置键（支持点号路径）</param>
    /// <param name="value">配置值</param>
    [ScriptMember("set")]
    public void Set(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        // 获取旧值用于事件发射
        object? oldValue = null;
        var hasOldValue = false;
        try
        {
            oldValue = _config.GetRaw(key);
            hasOldValue = true;
        }
        catch
        {
            // 键不存在，oldValue 保持为 null
        }

        // 设置新值
        _config.Set(key, value);

        var saveResult = _config.SaveToFile();
        if (!saveResult.IsSuccess)
        {
            _logService.Error(nameof(ConfigApi), "Failed to save config for key '{Key}': {ErrorCode}", key,
                              saveResult.Error?.Code ?? "UNKNOWN");
            return;
        }

        // 发射配置变更事件（仅当值实际发生变化时）
        if (_eventManager != null)
        {
            // 检查值是否真的发生了变化
            var valueChanged = !hasOldValue || !Equals(oldValue, value);
            if (valueChanged)
            {
                try
                {
                    var eventData = new Dictionary<string, object?> { { "key", key },
                                                                      { "newValue", value },
                                                                      { "oldValue", oldValue } };
                    _eventManager.Emit(EventManager.ConfigChanged, eventData);
                }
                catch (Exception ex)
                {
                    _logService.Error(nameof(ConfigApi), "Failed to emit configChanged event: {ErrorMessage}", ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// 检查配置键是否存在
    /// </summary>
    /// <param name="key">配置键</param>
    /// <returns>是否存在</returns>
    [ScriptMember("has")]
    public bool Has(string key)
    {
        return _config.ContainsKey(key);
    }

    /// <summary>
    /// 移除配置键
    /// </summary>
    /// <param name="key">配置键</param>
    /// <returns>是否成功移除</returns>
    [ScriptMember("remove")]
    public bool Remove(string key)
    {
        var result = _config.Remove(key);
        if (result)
        {
            var saveResult = _config.SaveToFile();
            if (!saveResult.IsSuccess)
            {
                _logService.Error(nameof(ConfigApi), "Failed to save config after remove '{Key}': {ErrorCode}", key,
                                  saveResult.Error?.Code ?? "UNKNOWN");
                return false;
            }
        }
        return result;
    }

    /// <summary>
    /// 注册配置变更事件监听器
    /// </summary>
    /// <param name="eventName">事件名称（目前仅支持 "change"）</param>
    /// <param name="callback">回调函数，接收 (key, newValue, oldValue) 参数</param>
    /// <returns>监听器 ID，用于取消监听</returns>
    [ScriptMember("on")]
    public int On(string eventName, object callback)
    {
        if (_eventManager == null)
        {
            _logService.Warn(nameof(ConfigApi), "EventManager not available, cannot register listener");
            return -1;
        }

        if (string.IsNullOrWhiteSpace(eventName))
            return -1;

        // 将 "change" 映射到内部事件名
        var internalEventName = eventName.ToLowerInvariant() switch { "change" => EventManager.ConfigChanged,
                                                                      _ => $"config.{eventName}" };

        return _eventManager.On(internalEventName, callback);
    }

    /// <summary>
    /// 取消配置变更事件监听
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="id">监听器 ID（可选，不提供则取消该事件的所有监听器）</param>
    [ScriptMember("off")]
    public void Off(string eventName, int? id = null)
    {
        if (_eventManager == null)
            return;

        if (id.HasValue)
        {
            _eventManager.Off(id.Value);
        }
        else if (!string.IsNullOrWhiteSpace(eventName))
        {
            var internalEventName = eventName.ToLowerInvariant() switch { "change" => EventManager.ConfigChanged,
                                                                          _ => $"config.{eventName}" };
            _eventManager.Off(internalEventName);
        }
    }

#endregion
}
}
