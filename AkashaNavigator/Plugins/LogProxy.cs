using System;
using Microsoft.ClearScript;
using Serilog;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 插件日志代理
/// 提供 debug/info/warn/error 方法，自动设置 SourceContext 为 Plugin:{PluginId}
/// </summary>
public class LogProxy
{
#region Fields

    private readonly string _pluginId;
    private readonly ILogger _logger;

#endregion

#region Constructor

    /// <summary>
    /// 创建插件日志代理实例
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public LogProxy(string pluginId)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        _logger = Log.ForContext("SourceContext", $"Plugin:{pluginId}");
    }

#endregion

#region Methods

    /// <summary>
    /// 输出 Debug 级别日志
    /// </summary>
    /// <param name="message">日志消息或消息模板</param>
    /// <param name="args">模板参数（可选）</param>
    [ScriptMember("debug")]
    public void Debug(object message, params object[] args)
    {
        var template = message?.ToString() ?? "null";
        if (args != null && args.Length > 0)
        {
            _logger.Debug(template, args);
        }
        else
        {
            _logger.Debug(template);
        }
    }

    /// <summary>
    /// 输出 Info 级别日志
    /// </summary>
    /// <param name="message">日志消息或消息模板</param>
    /// <param name="args">模板参数（可选）</param>
    [ScriptMember("info")]
    public void Info(object message, params object[] args)
    {
        var template = message?.ToString() ?? "null";
        if (args != null && args.Length > 0)
        {
            _logger.Information(template, args);
        }
        else
        {
            _logger.Information(template);
        }
    }

    /// <summary>
    /// 输出 Warn 级别日志
    /// </summary>
    /// <param name="message">日志消息或消息模板</param>
    /// <param name="args">模板参数（可选）</param>
    [ScriptMember("warn")]
    public void Warn(object message, params object[] args)
    {
        var template = message?.ToString() ?? "null";
        if (args != null && args.Length > 0)
        {
            _logger.Warning(template, args);
        }
        else
        {
            _logger.Warning(template);
        }
    }

    /// <summary>
    /// 输出 Error 级别日志
    /// </summary>
    /// <param name="message">日志消息或消息模板</param>
    /// <param name="args">模板参数（可选）</param>
    [ScriptMember("error")]
    public void Error(object message, params object[] args)
    {
        var template = message?.ToString() ?? "null";
        if (args != null && args.Length > 0)
        {
            _logger.Error(template, args);
        }
        else
        {
            _logger.Error(template);
        }
    }

#endregion
}
}
