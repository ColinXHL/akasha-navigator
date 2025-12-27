using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 插件关联管理器接口
    /// 管理插件与Profile的关联关系，支持双向查询
    /// </summary>
    public interface IPluginAssociationManager
    {
        /// <summary>
        /// 关联变化事件
        /// </summary>
        event EventHandler<AssociationChangedEventArgs>? AssociationChanged;

        /// <summary>
        /// 插件启用状态变化事件
        /// </summary>
        event EventHandler<PluginEnabledChangedEventArgs>? PluginEnabledChanged;

        /// <summary>
        /// 关联索引文件路径
        /// </summary>
        string AssociationsFilePath { get; }

        /// <summary>
        /// 重新加载索引
        /// </summary>
        void ReloadIndex();

        /// <summary>
        /// 获取使用指定插件的所有Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>使用该插件的Profile ID列表</returns>
        List<string> GetProfilesUsingPlugin(string pluginId);

        /// <summary>
        /// 获取插件被引用的次数
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>引用次数</returns>
        int GetPluginReferenceCount(string pluginId);

        /// <summary>
        /// 获取Profile引用的所有插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件引用列表</returns>
        List<PluginReference> GetPluginsInProfile(string profileId);

        /// <summary>
        /// 获取Profile中缺失的插件（引用但未安装）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>缺失的插件ID列表</returns>
        List<string> GetMissingPlugins(string profileId);

        /// <summary>
        /// 检查Profile是否包含指定插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否包含</returns>
        bool ProfileContainsPlugin(string profileId, string pluginId);

        /// <summary>
        /// 添加插件到Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <param name="enabled">是否启用（默认true）</param>
        /// <returns>是否成功添加（如果已存在则返回false）</returns>
        bool AddPluginToProfile(string pluginId, string profileId, bool enabled = true);

        /// <summary>
        /// 批量添加插件到Profile
        /// </summary>
        /// <param name="pluginIds">插件ID列表</param>
        /// <param name="profileId">Profile ID</param>
        void AddPluginsToProfile(List<string> pluginIds, string profileId);

        /// <summary>
        /// 从Profile移除插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功移除</returns>
        bool RemovePluginFromProfile(string pluginId, string profileId);

        /// <summary>
        /// 移除Profile的所有插件关联
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        void RemoveProfile(string profileId);

        /// <summary>
        /// 设置插件在Profile中的启用状态
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件ID</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>是否成功设置</returns>
        bool SetPluginEnabled(string profileId, string pluginId, bool enabled);
    }
}
