using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// Profile 管理服务接口
/// 负责加载、切换、保存 Profile 配置
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// Profile 切换事件
    /// </summary>
    event EventHandler<GameProfile>? ProfileChanged;

    /// <summary>
    /// 当前激活的 Profile
    /// </summary>
    GameProfile CurrentProfile { get; }

    /// <summary>
    /// 所有已加载的 Profile 列表
    /// </summary>
    List<GameProfile> Profiles { get; }

    /// <summary>
    /// 已安装的 Profile 只读列表
    /// </summary>
    IReadOnlyList<GameProfile> InstalledProfiles { get; }

    /// <summary>
    /// 数据根目录
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Profiles 目录
    /// </summary>
    string ProfilesDirectory { get; }

    /// <summary>
    /// 切换到指定 Profile
    /// </summary>
    bool SwitchProfile(string profileId);

    /// <summary>
    /// 根据 ID 获取 Profile
    /// </summary>
    GameProfile? GetProfileById(string id);

    /// <summary>
    /// 获取当前 Profile 的数据目录
    /// </summary>
    string GetCurrentProfileDirectory();

    /// <summary>
    /// 获取指定 Profile 的数据目录
    /// </summary>
    string GetProfileDirectory(string profileId);

    /// <summary>
    /// 保存当前 Profile 配置
    /// </summary>
    void SaveCurrentProfile();

    /// <summary>
    /// 保存指定 Profile 配置
    /// </summary>
    void SaveProfile(GameProfile profile);

    /// <summary>
    /// 取消订阅 Profile（删除 Profile 目录）
    /// </summary>
    UnsubscribeResult UnsubscribeProfile(string profileId);

    /// <summary>
    /// 重新加载所有 Profile
    /// </summary>
    void ReloadProfiles();

    /// <summary>
    /// 预定义的 Profile 图标列表
    /// </summary>
    string[] ProfileIcons { get; }

    /// <summary>
    /// 创建新的 Profile
    /// </summary>
    /// <returns>成功时返回 ProfileId，失败时返回错误信息</returns>
    Result<string> CreateProfile(string? id, string name, string icon, List<string>? pluginIds);

    /// <summary>
    /// 更新 Profile 名称和图标
    /// </summary>
    bool UpdateProfile(string id, string newName, string newIcon);

    /// <summary>
    /// 更新 Profile（扩展版本，支持更多字段）
    /// 支持部分更新：只更新 ProfileUpdateData 中非 null 的字段
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <param name="updateData">更新数据，null 字段表示不更新</param>
    /// <returns>是否成功更新</returns>
    bool UpdateProfile(string id, ProfileUpdateData updateData);

    /// <summary>
    /// 删除 Profile
    /// </summary>
    /// <returns>成功时返回 Result.Success()，失败时返回错误信息</returns>
    Result DeleteProfile(string id);

    /// <summary>
    /// 检查是否是默认 Profile
    /// </summary>
    bool IsDefaultProfile(string id);

    /// <summary>
    /// 检查 Profile ID 是否已存在
    /// </summary>
    bool ProfileIdExists(string id);

    /// <summary>
    /// 根据名称生成 Profile ID
    /// </summary>
    string GenerateProfileId(string name);

    /// <summary>
    /// 订阅 Profile（调用 SubscriptionManager）
    /// </summary>
    bool SubscribeProfile(string profileId);

    /// <summary>
    /// 取消订阅 Profile（调用 SubscriptionManager）
    /// </summary>
    UnsubscribeResult UnsubscribeProfileViaSubscription(string profileId);

    /// <summary>
    /// 导出 Profile（仅清单+配置，不含插件本体）
    /// </summary>
    ProfileExportData? ExportProfile(string profileId);

    /// <summary>
    /// 导出 Profile 到文件
    /// </summary>
    bool ExportProfileToFile(string profileId, string filePath);

    /// <summary>
    /// 导入 Profile（检查缺失插件）
    /// </summary>
    ProfileImportResult ImportProfile(ProfileExportData data, bool overwrite = false);

    /// <summary>
    /// 从文件导入 Profile
    /// </summary>
    ProfileImportResult ImportProfileFromFile(string filePath, bool overwrite = false);

    /// <summary>
    /// 预览导入（不实际导入，只检查缺失插件）
    /// </summary>
    ProfileImportResult PreviewImport(ProfileExportData data);

    /// <summary>
    /// 获取 Profile 的插件引用清单
    /// </summary>
    List<PluginReference> GetPluginReferences(string profileId);

    /// <summary>
    /// 设置插件在 Profile 中的启用状态
    /// </summary>
    bool SetPluginEnabled(string profileId, string pluginId, bool enabled);

    /// <summary>
    /// 获取插件的 Profile 特定配置
    /// </summary>
    Dictionary<string, object>? GetPluginConfig(string profileId, string pluginId);

    /// <summary>
    /// 保存插件的 Profile 特定配置
    /// </summary>
    bool SavePluginConfig(string profileId, string pluginId, Dictionary<string, object> config);

    /// <summary>
    /// 删除插件的 Profile 特定配置
    /// </summary>
    bool DeletePluginConfig(string profileId, string pluginId);

    /// <summary>
    /// 获取 Profile 的插件配置目录
    /// </summary>
    string GetPluginConfigsDirectory(string profileId);

    /// <summary>
    /// 获取 Profile 中所有插件的配置
    /// </summary>
    Dictionary<string, Dictionary<string, object>> GetAllPluginConfigs(string profileId);
}
}
