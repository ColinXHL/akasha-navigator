using System;
using System.Collections.Generic;
using System.Text.Json;
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
    /// <returns>成功添加的数量</returns>
    int AddPluginsToProfile(IEnumerable<string> pluginIds, string profileId);

    /// <summary>
    /// 批量添加插件到多个Profile
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="profileIds">Profile ID列表</param>
    /// <returns>成功添加的数量</returns>
    int AddPluginToProfiles(string pluginId, IEnumerable<string> profileIds);

    /// <summary>
    /// 从Profile移除插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="profileId">Profile ID</param>
    /// <returns>是否成功移除</returns>
    bool RemovePluginFromProfile(string pluginId, string profileId);

    /// <summary>
    /// 从所有Profile中移除指定插件的引用
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>被移除引用的Profile数量</returns>
    int RemovePluginFromAllProfiles(string pluginId);

    /// <summary>
    /// 移除Profile的所有插件关联
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>是否成功删除</returns>
    bool RemoveProfile(string profileId);

    /// <summary>
    /// 设置插件在Profile中的启用状态
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件ID</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>是否成功设置</returns>
    bool SetPluginEnabled(string profileId, string pluginId, bool enabled);

    /// <summary>
    /// 获取插件在Profile中的启用状态
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否启用（如果不存在则返回null）</returns>
    bool? GetPluginEnabled(string profileId, string pluginId);

    /// <summary>
    /// 设置 Profile 的原始插件列表（来自市场定义）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginIds">原始插件 ID 列表</param>
    void SetOriginalPlugins(string profileId, List<string> pluginIds);

    /// <summary>
    /// 获取 Profile 的原始插件列表
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>原始插件 ID 列表，如果没有则返回空列表</returns>
    List<string> GetOriginalPlugins(string profileId);

    /// <summary>
    /// 检查 Profile 是否有原始插件列表（判断是否来自市场）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>是否有原始插件列表</returns>
    bool HasOriginalPlugins(string profileId);

    /// <summary>
    /// 获取 Profile 中缺失的原始插件（在原始列表中但不在当前关联中）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>缺失的插件 ID 列表</returns>
    List<string> GetMissingOriginalPlugins(string profileId);

    /// <summary>
    /// 删除 Profile 的原始插件列表
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    void RemoveOriginalPlugins(string profileId);

    /// <summary>
    /// 应用 Profile 的插件预设配置
    /// 将 Profile 中定义的 pluginConfigs 应用到对应的插件配置目录
    /// 如果配置文件已存在，则不覆盖
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="presetConfigs">预设配置字典，Key: 插件ID, Value: 配置对象</param>
    void ApplyPluginPresetConfigs(string profileId, Dictionary<string, Dictionary<string, JsonElement>>? presetConfigs);

    /// <summary>
    /// 获取所有有关联的Profile ID列表
    /// </summary>
    /// <returns>Profile ID列表</returns>
    List<string> GetAllProfileIds();
}
}
