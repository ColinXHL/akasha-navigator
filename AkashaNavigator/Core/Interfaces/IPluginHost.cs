using System;
using System.Collections.Generic;
using AkashaNavigator.Plugins.Core;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 插件宿主接口
    /// 负责插件的加载、执行和生命周期管理
    /// </summary>
    public interface IPluginHost : IDisposable
    {
        /// <summary>
        /// 插件加载完成事件
        /// </summary>
        event EventHandler<PluginContext>? PluginLoaded;

        /// <summary>
        /// 插件卸载事件
        /// </summary>
        event EventHandler<string>? PluginUnloaded;

        /// <summary>
        /// 已加载的插件列表
        /// </summary>
        IReadOnlyList<PluginContext> LoadedPlugins { get; }

        /// <summary>
        /// 当前 Profile ID
        /// </summary>
        string? CurrentProfileId { get; }

        /// <summary>
        /// 加载指定 Profile 的所有插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        void LoadPluginsForProfile(string profileId);

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        void UnloadAllPlugins();

        /// <summary>
        /// 启用或禁用插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="enabled">是否启用</param>
        void SetPluginEnabled(string pluginId, bool enabled);

        /// <summary>
        /// 根据 ID 获取插件
        /// </summary>
        PluginContext? GetPlugin(string pluginId);

        /// <summary>
        /// 获取插件配置
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        AkashaNavigator.Models.Plugin.PluginConfig? GetPluginConfig(string pluginId);

        /// <summary>
        /// 保存插件配置
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        void SavePluginConfig(string pluginId);

        /// <summary>
        /// 广播事件到所有启用的插件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        void BroadcastEvent(string eventName, object data);

        /// <summary>
        /// 广播播放状态变化事件
        /// </summary>
        /// <param name="playing">是否正在播放</param>
        void BroadcastPlayStateChanged(bool playing);

        /// <summary>
        /// 广播时间更新事件
        /// </summary>
        /// <param name="currentTime">当前时间（秒）</param>
        /// <param name="duration">总时长（秒）</param>
        void BroadcastTimeUpdate(double currentTime, double duration);

        /// <summary>
        /// 广播 URL 变化事件
        /// </summary>
        /// <param name="url">新 URL</param>
        void BroadcastUrlChanged(string url);

        /// <summary>
        /// 动态启用插件（如果当前 Profile 匹配）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        void EnablePlugin(string profileId, string pluginId);

        /// <summary>
        /// 重新加载指定插件（用于更新后）
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        void ReloadPlugin(string pluginId);

        /// <summary>
        /// 获取插件配置目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        string GetPluginConfigDirectory(string profileId, string pluginId);
    }
}
