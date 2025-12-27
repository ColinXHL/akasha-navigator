using System;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 日志服务接口
    /// 提供结构化的日志记录功能
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// 日志目录路径
        /// </summary>
        string LogDirectory { get; }

        /// <summary>
        /// 记录 Debug 级别日志
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="message">日志消息</param>
        void Debug(string source, string message);

        /// <summary>
        /// 记录 Debug 级别日志（参数化模板）
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="template">消息模板，支持 {Name} 占位符</param>
        /// <param name="args">模板参数</param>
        void Debug(string source, string template, params object?[] args);

        /// <summary>
        /// 记录 Info 级别日志
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="message">日志消息</param>
        void Info(string source, string message);

        /// <summary>
        /// 记录 Info 级别日志（参数化模板）
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="template">消息模板，支持 {Name} 占位符</param>
        /// <param name="args">模板参数</param>
        void Info(string source, string template, params object?[] args);

        /// <summary>
        /// 记录 Warn 级别日志
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="message">日志消息</param>
        void Warn(string source, string message);

        /// <summary>
        /// 记录 Warn 级别日志（参数化模板）
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="template">消息模板，支持 {Name} 占位符</param>
        /// <param name="args">模板参数</param>
        void Warn(string source, string template, params object?[] args);

        /// <summary>
        /// 记录 Error 级别日志
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="message">日志消息</param>
        void Error(string source, string message);

        /// <summary>
        /// 记录 Error 级别日志（参数化模板）
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="template">消息模板，支持 {Name} 占位符</param>
        /// <param name="args">模板参数</param>
        void Error(string source, string template, params object?[] args);

        /// <summary>
        /// 记录 Error 级别日志（包含异常和参数化模板）
        /// </summary>
        /// <param name="source">来源组件名称</param>
        /// <param name="ex">异常对象</param>
        /// <param name="template">消息模板，支持 {Name} 占位符</param>
        /// <param name="args">模板参数</param>
        void Error(string source, Exception ex, string template, params object?[] args);
    }
}
