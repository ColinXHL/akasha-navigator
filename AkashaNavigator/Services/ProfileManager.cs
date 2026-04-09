using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// Profile 管理服务
/// 负责加载、切换、保存 Profile 配置
/// 集成订阅机制：只加载已订阅的 Profile
/// </summary>
public class ProfileManager : IProfileManager
{
#region Fields

    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly IPluginHost _pluginHost;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IProfileRegistry _profileRegistry;

#endregion

#region Events

    /// <summary>
    /// Profile 切换事件
    /// </summary>
    public event EventHandler<GameProfile>? ProfileChanged;

#endregion

#region Properties

    /// <summary>
    /// 当前激活的 Profile
    /// </summary>
    public GameProfile CurrentProfile { get; private set; }

    /// <summary>
    /// 所有已加载的 Profile 列表
    /// </summary>
    public List<GameProfile> Profiles { get; } = new();

    /// <summary>
    /// 已安装的 Profile 只读列表
    /// </summary>
    public IReadOnlyList<GameProfile> InstalledProfiles => Profiles.AsReadOnly();

    /// <summary>
    /// 数据根目录
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Profiles 目录
    /// </summary>
    public string ProfilesDirectory { get; }

#endregion

#region Constructor

    /// <summary>
    /// 私有构造函数（单例模式 + DI）
    /// </summary>
    public ProfileManager(IConfigService configService, ILogService logService, IPluginHost pluginHost,
                          IPluginAssociationManager pluginAssociationManager, ISubscriptionManager subscriptionManager,
                          IPluginLibrary pluginLibrary, IProfileRegistry profileRegistry)
    {
        _configService = configService;
        _logService = logService;
        _pluginHost = pluginHost;
        _pluginAssociationManager = pluginAssociationManager;
        _subscriptionManager = subscriptionManager;
        _pluginLibrary = pluginLibrary;
        _profileRegistry = profileRegistry;

        // 数据目录：User/Data/
        DataDirectory = AppPaths.DataDirectory;
        ProfilesDirectory = AppPaths.ProfilesDirectory;

        // 加载所有 Profile
        LoadAllProfiles();

        // 从配置中恢复上次选择的 Profile，如果不存在则使用默认 Profile
        var savedProfileId = _configService.Config.CurrentProfileId;
        CurrentProfile =
            GetProfileById(savedProfileId) ?? GetProfileById(AppConstants.DefaultProfileId) ?? CreateDefaultProfile();
    }

#endregion

#region Public Methods

    /// <summary>
    /// 切换到指定 Profile
    /// </summary>
    public bool SwitchProfile(string profileId)
    {
        var profile = GetProfileById(profileId);
        if (profile == null)
            return false;

        // 卸载当前 Profile 的插件
        _pluginHost.UnloadAllPlugins();

        CurrentProfile = profile;

        // 保存当前选择的 Profile ID 到配置
        var config = _configService.Config;
        config.CurrentProfileId = profileId;
        _configService.Save();

        // 加载新 Profile 的插件
        _pluginHost.LoadPluginsForProfile(profileId);

        // 广播 profileChanged 事件到插件
        _pluginHost.BroadcastEvent(EventManager.ProfileChanged, new { profileId = profile.Id });

        ProfileChanged?.Invoke(this, profile);
        return true;
    }

    /// <summary>
    /// 根据 ID 获取 Profile
    /// </summary>
    public GameProfile? GetProfileById(string id)
    {
        return Profiles.Find(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取当前 Profile 的数据目录
    /// </summary>
    public string GetCurrentProfileDirectory()
    {
        return GetProfileDirectory(CurrentProfile.Id);
    }

    /// <summary>
    /// 获取指定 Profile 的数据目录
    /// </summary>
    public string GetProfileDirectory(string profileId)
    {
        return Path.Combine(ProfilesDirectory, profileId);
    }

    /// <summary>
    /// 保存当前 Profile 配置
    /// </summary>
    public void SaveCurrentProfile()
    {
        SaveProfile(CurrentProfile);
    }

    /// <summary>
    /// 保存指定 Profile 配置
    /// </summary>
    public void SaveProfile(GameProfile profile)
    {
        var saveResult = SaveProfileToDisk(profile);
        if (!saveResult.IsSuccess)
        {
            _logService.Warn(nameof(ProfileManager), "保存 Profile 失败 [{ProfileId}]: {ErrorCode}", profile.Id,
                             saveResult.Error?.Code ?? "UNKNOWN");
        }
    }

    /// <summary>
    /// 取消订阅 Profile（删除 Profile 目录）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>操作结果</returns>
    public UnsubscribeResult UnsubscribeProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return UnsubscribeResult.Failed("Profile ID 不能为空");
        }

        // 不允许删除默认 Profile
        if (profileId.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return UnsubscribeResult.Failed("不能删除默认 Profile");
        }

        // 查找 Profile
        var profile = GetProfileById(profileId);
        if (profile == null)
        {
            // Profile 不存在，静默成功
            return UnsubscribeResult.Succeeded();
        }

        var profileDir = GetProfileDirectory(profileId);

        try
        {
            // 如果是当前 Profile，先切换到默认 Profile
            if (CurrentProfile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
            {
                SwitchProfile(AppConstants.DefaultProfileId);
            }
            else
            {
                // 卸载该 Profile 的插件（如果有加载的话）
                // 注意：由于我们已经切换了 Profile，这里不需要额外卸载
            }

            // 从列表中移除
            Profiles.RemoveAll(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));

            // 删除 Profile 目录
            if (Directory.Exists(profileDir))
            {
                Directory.Delete(profileDir, recursive: true);
            }

            return UnsubscribeResult.Succeeded();
        }
        catch (UnauthorizedAccessException ex)
        {
            return UnsubscribeResult.Failed($"删除 Profile 目录失败：权限不足。{ex.Message}");
        }
        catch (IOException ex)
        {
            return UnsubscribeResult.Failed($"删除 Profile 目录失败：文件被占用。{ex.Message}");
        }
        catch (Exception ex)
        {
            return UnsubscribeResult.Failed($"取消订阅失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 重新加载所有 Profile
    /// </summary>
    public void ReloadProfiles()
    {
        Profiles.Clear();
        LoadAllProfiles();

        // 如果当前 Profile 不存在，切换到 Default
        if (GetProfileById(CurrentProfile.Id) == null)
        {
            CurrentProfile = GetProfileById(AppConstants.DefaultProfileId) ?? CreateDefaultProfile();
            ProfileChanged?.Invoke(this, CurrentProfile);
        }
    }

#endregion

#region Profile CRUD Operations

    /// <summary>
    /// 预定义的 Profile 图标列表
    /// </summary>
    public static readonly string[] ProfileIcons = new[] { "📦", "🎮", "🎬", "📺", "🎵", "📚", "🎯", "⚡", "🔧", "💡" };

    /// <summary>
    /// 预定义的 Profile 图标列表（接口实现）
    /// </summary>
    string[] IProfileManager.ProfileIcons => ProfileIcons;

    /// <summary>
    /// 创建新的 Profile
    /// </summary>
    /// <param name="id">Profile ID（如果为空则自动生成）</param>
    /// <param name="name">Profile 名称</param>
    /// <param name="icon">Profile 图标</param>
    /// <param name="pluginIds">要关联的插件 ID 列表</param>
    /// <returns>成功时返回 ProfileId，失败时返回错误信息</returns>
    public Result<string> CreateProfile(string? id, string name, string icon, List<string>? pluginIds)
    {
        // 验证名称
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<string>.Failure(Error.Validation(ProfileErrorCodes.NameEmpty, "Profile 名称不能为空"));
        }

        // 生成或验证 ID
        var profileId = string.IsNullOrWhiteSpace(id) ? GenerateProfileId(name) : id;

        // 检查 ID 是否已存在
        if (ProfileIdExists(profileId))
        {
            return Result<string>.Failure(Error.Validation(ProfileErrorCodes.AlreadyExists, "已存在同名 Profile"));
        }

        // 验证图标
        if (string.IsNullOrWhiteSpace(icon))
        {
            icon = "📦";
        }

        try
        {
            // 创建 Profile 对象
            var profile =
                new GameProfile { Id = profileId, Name = name.Trim(), Icon = icon, Version = 1,
                                  Defaults = new ProfileDefaults { Url = AppConstants.DefaultHomeUrl,
                                                                   SeekSeconds = AppConstants.DefaultSeekSeconds } };

            // 创建 Profile 目录和配置文件
            var profileDir = GetProfileDirectory(profileId);
            Directory.CreateDirectory(profileDir);
            SaveProfile(profile);

            // 添加到订阅
            AddProfileToSubscription(profileId);

            // 添加到内存列表
            Profiles.Add(profile);

            // 关联插件
            if (pluginIds != null && pluginIds.Count > 0)
            {
                _pluginAssociationManager.AddPluginsToProfile(pluginIds, profileId);
            }

            _logService.Info(nameof(ProfileManager), "成功创建 Profile '{ProfileId}'", profileId);
            return Result<string>.Success(profileId);
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "创建 Profile 失败");
            return Result<string>.Failure(Error.Unknown("PROFILE_CREATE_FAILED", $"创建失败: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// 更新 Profile 名称和图标
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <param name="newName">新名称</param>
    /// <param name="newIcon">新图标</param>
    /// <returns>是否成功</returns>
    public bool UpdateProfile(string id, string newName, string newIcon)
    {
        // 验证名称
        if (string.IsNullOrWhiteSpace(newName))
        {
            _logService.Warn(nameof(ProfileManager), "更新 Profile 失败: 名称不能为空");
            return false;
        }

        // 查找 Profile
        var profile = GetProfileById(id);
        if (profile == null)
        {
            _logService.Warn(nameof(ProfileManager), "更新 Profile 失败: Profile '{ProfileId}' 不存在", id);
            return false;
        }

        try
        {
            // 更新属性
            profile.Name = newName.Trim();
            if (!string.IsNullOrWhiteSpace(newIcon))
            {
                profile.Icon = newIcon;
            }

            // 保存到文件
            SaveProfile(profile);

            _logService.Info(nameof(ProfileManager), "成功更新 Profile '{ProfileId}'", id);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "更新 Profile 失败");
            return false;
        }
    }

    /// <summary>
    /// 更新 Profile（扩展版本，支持更多字段）
    /// 支持部分更新：只更新 ProfileUpdateData 中非 null 的字段
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <param name="updateData">更新数据，null 字段表示不更新</param>
    /// <returns>是否成功更新</returns>
    public bool UpdateProfile(string id, ProfileUpdateData updateData)
    {
        if (updateData == null)
        {
            _logService.Warn(nameof(ProfileManager), "更新 Profile 失败: updateData 为空");
            return false;
        }

        // 查找 Profile
        var profile = GetProfileById(id);
        if (profile == null)
        {
            _logService.Warn(nameof(ProfileManager), "更新 Profile 失败: Profile '{ProfileId}' 不存在", id);
            return false;
        }

        try
        {
            // 部分更新：只更新非 null 的字段
            if (updateData.Name != null)
            {
                if (string.IsNullOrWhiteSpace(updateData.Name))
                {
                    _logService.Warn(nameof(ProfileManager), "更新 Profile 失败: 名称不能为空");
                    return false;
                }
                profile.Name = updateData.Name.Trim();
            }

            if (updateData.Icon != null)
            {
                profile.Icon = updateData.Icon;
            }

            if (updateData.Defaults != null)
            {
                profile.Defaults = updateData.Defaults;
            }

            // 保存到文件
            SaveProfile(profile);

            _logService.Info(nameof(ProfileManager), "成功更新 Profile '{ProfileId}'（扩展更新）", id);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "更新 Profile 失败");
            return false;
        }
    }

    /// <summary>
    /// 删除 Profile
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <returns>成功时返回 Result.Success()，失败时返回错误信息</returns>
    public Result DeleteProfile(string id)
    {
        // 验证 ID
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result.Failure(Error.Validation(ProfileErrorCodes.NameEmpty, "Profile ID 不能为空"));
        }

        // 不允许删除默认 Profile
        if (IsDefaultProfile(id))
        {
            return Result.Failure(Error.Validation(ProfileErrorCodes.IsDefault, "默认 Profile 不能删除"));
        }

        // 查找 Profile
        var profile = GetProfileById(id);
        if (profile == null)
        {
            // Profile 不存在，静默成功
            return Result.Success();
        }

        try
        {
            // 如果是当前 Profile，先切换到默认 Profile
            if (CurrentProfile.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                SwitchProfile(AppConstants.DefaultProfileId);
            }

            // 删除插件关联
            _pluginAssociationManager.RemoveProfile(id);

            // 从订阅中移除
            RemoveProfileFromSubscription(id);

            // 从内存列表中移除
            Profiles.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            // 删除 Profile 目录
            var profileDir = GetProfileDirectory(id);
            if (Directory.Exists(profileDir))
            {
                Directory.Delete(profileDir, recursive: true);
            }

            _logService.Info(nameof(ProfileManager), "成功删除 Profile '{ProfileId}'", id);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure(Error.FileSystem(ProfileErrorCodes.DeleteFailed,
                                                   $"删除 Profile 目录失败：权限不足。{ex.Message}",
                                                   filePath: GetProfileDirectory(id)));
        }
        catch (IOException ex)
        {
            return Result.Failure(Error.FileSystem(ProfileErrorCodes.DeleteFailed,
                                                   $"删除 Profile 目录失败：文件被占用。{ex.Message}",
                                                   filePath: GetProfileDirectory(id)));
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "删除 Profile 失败");
            return Result.Failure(Error.Unknown(ProfileErrorCodes.DeleteFailed, $"删除失败: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// 检查是否是默认 Profile
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <returns>是否是默认 Profile</returns>
    public bool IsDefaultProfile(string id)
    {
        return id.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查 Profile ID 是否已存在
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <returns>是否存在</returns>
    public bool ProfileIdExists(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return GetProfileById(id) != null;
    }

    /// <summary>
    /// 根据名称生成 Profile ID
    /// </summary>
    /// <param name="name">Profile 名称</param>
    /// <returns>生成的 ID</returns>
    public string GenerateProfileId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"profile-{Guid.NewGuid():N}".Substring(0, 16);
        }

        // 将名称转换为 kebab-case ID
        var id = name.Trim().ToLowerInvariant();

        // 替换空格和特殊字符为连字符
        id = Regex.Replace(id, @"[^a-z0-9\u4e00-\u9fa5]", "-");

        // 移除连续的连字符
        id = Regex.Replace(id, @"-+", "-");

        // 移除首尾连字符
        id = id.Trim('-');

        // 如果 ID 为空或太短，添加随机后缀
        if (string.IsNullOrEmpty(id) || id.Length < 2)
        {
            id = $"profile-{Guid.NewGuid():N}".Substring(0, 16);
        }

        // 如果 ID 已存在，添加数字后缀
        var baseId = id;
        var counter = 1;
        while (ProfileIdExists(id))
        {
            id = $"{baseId}-{counter}";
            counter++;
        }

        return id;
    }

    /// <summary>
    /// 将 Profile 添加到订阅配置
    /// </summary>
    private void AddProfileToSubscription(string profileId)
    {
        var subscriptionsPath = AppPaths.SubscriptionsFilePath;
        var config = new SubscriptionConfig();

        if (File.Exists(subscriptionsPath))
        {
            try
            {
                config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
            }
            catch
            {
                config = new SubscriptionConfig();
            }
        }

        if (!config.IsProfileSubscribed(profileId))
        {
            config.AddProfile(profileId);
            config.SaveToFile(subscriptionsPath);
        }

        // 重新加载 SubscriptionManager
        _subscriptionManager.Load();
    }

    /// <summary>
    /// 从订阅配置中移除 Profile
    /// </summary>
    private void RemoveProfileFromSubscription(string profileId)
    {
        var subscriptionsPath = AppPaths.SubscriptionsFilePath;

        if (!File.Exists(subscriptionsPath))
            return;

        try
        {
            var config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
            config.RemoveProfile(profileId);
            config.SaveToFile(subscriptionsPath);

            // 重新加载 SubscriptionManager
            _subscriptionManager.Load();
        }
        catch (Exception ex)
        {
            _logService.Warn(nameof(ProfileManager), "从订阅配置移除 Profile 失败: {ErrorMessage}", ex.Message);
        }
    }

#endregion

#region Subscription Methods

    /// <summary>
    /// 订阅 Profile（调用 SubscriptionManager）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>是否成功</returns>
    public bool SubscribeProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            _logService.Warn(nameof(ProfileManager), "订阅 Profile 失败: profileId 为空");
            return false;
        }

        // 调用 SubscriptionManager 执行订阅
        var success = _subscriptionManager.SubscribeProfile(profileId);

        if (success)
        {
            // 重新加载 Profiles 列表
            ReloadProfiles();
            _logService.Info(nameof(ProfileManager), "成功订阅 Profile '{ProfileId}'", profileId);
        }

        return success;
    }

    /// <summary>
    /// 取消订阅 Profile（调用 SubscriptionManager）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>取消订阅结果</returns>
    public UnsubscribeResult UnsubscribeProfileViaSubscription(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return UnsubscribeResult.Failed("Profile ID 不能为空");
        }

        // 不允许取消订阅默认 Profile
        if (profileId.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return UnsubscribeResult.Failed("不能取消订阅默认 Profile");
        }

        // 如果是当前 Profile，先切换到默认 Profile
        if (CurrentProfile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
        {
            SwitchProfile(AppConstants.DefaultProfileId);
        }

        // 调用 SubscriptionManager 执行取消订阅
        var result = _subscriptionManager.UnsubscribeProfile(profileId);

        if (result.IsSuccess)
        {
            // 从列表中移除
            Profiles.RemoveAll(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
            _logService.Info(nameof(ProfileManager), "成功取消订阅 Profile '{ProfileId}'", profileId);
        }

        return result;
    }

#endregion

#region Profile Import / Export(导入导出)

    /// <summary>
    /// 导出 Profile（仅清单+配置，不含插件本体）
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>导出数据，如果 Profile 不存在则返回 null</returns>
    public ProfileExportData? ExportProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return null;

        // 获取 Profile 配置
        var profile = GetProfileById(profileId);
        if (profile == null)
        {
            _logService.Warn(nameof(ProfileManager), "导出失败：Profile '{ProfileId}' 不存在", profileId);
            return null;
        }

        // 获取插件引用清单
        var pluginReferences = _pluginAssociationManager.GetPluginsInProfile(profileId);
        var referenceEntries = pluginReferences.Select(r => PluginReferenceEntry.FromReference(r)).ToList();

        // 获取所有插件配置
        var pluginConfigs = GetAllPluginConfigs(profileId);

        // 创建导出数据
        var exportData = new ProfileExportData { Version = 1,
                                                 ProfileId = profile.Id,
                                                 ProfileName = profile.Name,
                                                 ProfileConfig = profile,
                                                 PluginReferences = referenceEntries,
                                                 PluginConfigs = pluginConfigs,
                                                 ExportedAt = DateTime.Now };

        _logService.Info(nameof(ProfileManager),
                         "导出 Profile '{ProfileId}'：{ReferenceCount} 个插件引用，{ConfigCount} 个插件配置", profileId,
                         referenceEntries.Count, pluginConfigs.Count);

        return exportData;
    }

    /// <summary>
    /// 导出 Profile 到文件
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="filePath">目标文件路径</param>
    /// <returns>是否成功导出</returns>
    public bool ExportProfileToFile(string profileId, string filePath)
    {
        var exportData = ExportProfile(profileId);
        if (exportData == null)
            return false;

        try
        {
            exportData.SaveToFile(filePath);
            _logService.Info(nameof(ProfileManager), "Profile '{ProfileId}' 已导出到 {FilePath}", profileId, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "导出 Profile 到文件失败");
            return false;
        }
    }

    /// <summary>
    /// 导入 Profile（检查缺失插件）
    /// </summary>
    /// <param name="data">导出数据</param>
    /// <param name="overwrite">如果 Profile 已存在是否覆盖</param>
    /// <returns>导入结果</returns>
    public ProfileImportResult ImportProfile(ProfileExportData data, bool overwrite = false)
    {
        if (data == null)
            return ProfileImportResult.Failure("导入数据为空");

        if (string.IsNullOrWhiteSpace(data.ProfileId))
            return ProfileImportResult.Failure("Profile ID 为空");

        // 检查版本兼容性
        if (data.Version > 1)
        {
            return ProfileImportResult.Failure($"不支持的导出格式版本: {data.Version}");
        }

        // 检查 Profile 是否已存在
        var existingProfile = GetProfileById(data.ProfileId);
        if (existingProfile != null && !overwrite)
        {
            return ProfileImportResult.Exists(data.ProfileId);
        }

        // 检测缺失的插件
        var missingPlugins = new List<string>();
        foreach (var reference in data.PluginReferences)
        {
            if (!_pluginLibrary.IsInstalled(reference.PluginId))
            {
                missingPlugins.Add(reference.PluginId);
            }
        }

        try
        {
            // 创建或更新 Profile 配置
            var profileDir = GetProfileDirectory(data.ProfileId);
            Directory.CreateDirectory(profileDir);

            // 保存 Profile 配置
            var profileConfig = data.ProfileConfig ?? new GameProfile { Id = data.ProfileId, Name = data.ProfileName };
            profileConfig.Id = data.ProfileId; // 确保 ID 一致
            SaveProfile(profileConfig);

            // 应用插件预设配置（如果有）
            if (profileConfig.PluginConfigs != null && profileConfig.PluginConfigs.Count > 0)
            {
                _pluginAssociationManager.ApplyPluginPresetConfigs(data.ProfileId, profileConfig.PluginConfigs);
            }

            // 创建插件关联
            foreach (var reference in data.PluginReferences)
            {
                _pluginAssociationManager.AddPluginToProfile(reference.PluginId, data.ProfileId, reference.Enabled);
            }

            // 保存插件配置
            foreach (var kvp in data.PluginConfigs)
            {
                SavePluginConfig(data.ProfileId, kvp.Key, kvp.Value);
            }

            // 添加到订阅
            if (!_subscriptionManager.IsProfileSubscribed(data.ProfileId))
            {
                // 手动添加到订阅配置
                var subscriptionsPath = AppPaths.SubscriptionsFilePath;
                var config = new SubscriptionConfig();

                if (File.Exists(subscriptionsPath))
                {
                    try
                    {
                        config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
                    }
                    catch
                    {
                        config = new SubscriptionConfig();
                    }
                }

                config.AddProfile(data.ProfileId);
                config.SaveToFile(subscriptionsPath);
                _subscriptionManager.Load();
            }

            // 重新加载 Profiles 列表
            ReloadProfiles();

            _logService.Info(nameof(ProfileManager),
                             "导入 Profile '{ProfileId}'：{ReferenceCount} 个插件引用，{MissingCount} 个缺失",
                             data.ProfileId, data.PluginReferences.Count, missingPlugins.Count);

            return ProfileImportResult.Success(data.ProfileId, missingPlugins);
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(ProfileManager), ex, "导入 Profile 失败");
            return ProfileImportResult.Failure($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从文件导入 Profile
    /// </summary>
    /// <param name="filePath">导入文件路径</param>
    /// <param name="overwrite">如果 Profile 已存在是否覆盖</param>
    /// <returns>导入结果</returns>
    public ProfileImportResult ImportProfileFromFile(string filePath, bool overwrite = false)
    {
        if (!File.Exists(filePath))
            return ProfileImportResult.Failure($"文件不存在: {filePath}");

        var data = ProfileExportData.LoadFromFile(filePath);
        if (data == null)
            return ProfileImportResult.Failure("无法解析导入文件");

        return ImportProfile(data, overwrite);
    }

    /// <summary>
    /// 预览导入（不实际导入，只检查缺失插件）
    /// </summary>
    /// <param name="data">导出数据</param>
    /// <returns>导入预览结果</returns>
    public ProfileImportResult PreviewImport(ProfileExportData data)
    {
        if (data == null)
            return ProfileImportResult.Failure("导入数据为空");

        if (string.IsNullOrWhiteSpace(data.ProfileId))
            return ProfileImportResult.Failure("Profile ID 为空");

        // 检查 Profile 是否已存在
        var existingProfile = GetProfileById(data.ProfileId);
        if (existingProfile != null)
        {
            var result = ProfileImportResult.Exists(data.ProfileId);
            // 仍然检测缺失插件
            foreach (var reference in data.PluginReferences)
            {
                if (!_pluginLibrary.IsInstalled(reference.PluginId))
                {
                    result.MissingPlugins.Add(reference.PluginId);
                }
            }
            return result;
        }

        // 检测缺失的插件
        var missingPlugins = new List<string>();
        foreach (var reference in data.PluginReferences)
        {
            if (!_pluginLibrary.IsInstalled(reference.PluginId))
            {
                missingPlugins.Add(reference.PluginId);
            }
        }

        return ProfileImportResult.Success(data.ProfileId, missingPlugins);
    }

#endregion

#region Plugin Reference Management(插件引用管理)

    /// <summary>
    /// 获取 Profile 的插件引用清单
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>插件引用列表</returns>
    public List<PluginReference> GetPluginReferences(string profileId)
    {
        return _pluginAssociationManager.GetPluginsInProfile(profileId);
    }

    /// <summary>
    /// 设置插件在 Profile 中的启用状态
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>是否成功设置</returns>
    public bool SetPluginEnabled(string profileId, string pluginId, bool enabled)
    {
        return _pluginAssociationManager.SetPluginEnabled(profileId, pluginId, enabled);
    }

    /// <summary>
    /// 获取插件的 Profile 特定配置
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>配置字典，如果不存在则返回 null</returns>
    public Dictionary<string, object>? GetPluginConfig(string profileId, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
            return null;

        var configPath = GetPluginConfigPath(profileId, pluginId);
        if (!File.Exists(configPath))
            return null;

        var result = JsonHelper.LoadFromFile<Dictionary<string, object>>(configPath);
        if (result.IsSuccess)
        {
            return result.Value;
        }
        else
        {
            _logService.Debug(nameof(ProfileManager), "加载插件配置失败 [{ConfigPath}]: {ErrorMessage}", configPath,
                              result.Error?.Message ?? "Unknown error");
            return null;
        }
    }

    /// <summary>
    /// 保存插件的 Profile 特定配置
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="config">配置字典</param>
    /// <returns>是否成功保存</returns>
    public bool SavePluginConfig(string profileId, string pluginId, Dictionary<string, object> config)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
            return false;

        var configPath = GetPluginConfigPath(profileId, pluginId);

        try
        {
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            JsonHelper.SaveToFile(configPath, config);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Debug(nameof(ProfileManager), "保存插件配置失败 [{ConfigPath}]: {ErrorMessage}", configPath,
                              ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 删除插件的 Profile 特定配置
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>是否成功删除</returns>
    public bool DeletePluginConfig(string profileId, string pluginId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
            return false;

        var configPath = GetPluginConfigPath(profileId, pluginId);

        try
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logService.Debug(nameof(ProfileManager), "删除插件配置失败 [{ConfigPath}]: {ErrorMessage}", configPath,
                              ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 获取插件配置文件路径
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>配置文件路径</returns>
    private string GetPluginConfigPath(string profileId, string pluginId)
    {
        var profileDir = GetProfileDirectory(profileId);
        return Path.Combine(profileDir, "plugin-configs", $"{pluginId}.json");
    }

    /// <summary>
    /// 获取 Profile 的插件配置目录
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>插件配置目录路径</returns>
    public string GetPluginConfigsDirectory(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), "plugin-configs");
    }

    /// <summary>
    /// 获取 Profile 中所有插件的配置
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>插件ID到配置的字典</returns>
    public Dictionary<string, Dictionary<string, object>> GetAllPluginConfigs(string profileId)
    {
        var result = new Dictionary<string, Dictionary<string, object>>();
        var configsDir = GetPluginConfigsDirectory(profileId);

        if (!Directory.Exists(configsDir))
            return result;

        try
        {
            foreach (var file in Directory.GetFiles(configsDir, "*.json"))
            {
                var pluginId = Path.GetFileNameWithoutExtension(file);
                var config = JsonHelper.LoadFromFile<Dictionary<string, object>>(file);
                if (config.IsSuccess)
                {
                    result[pluginId] = config.Value!;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Debug(nameof(ProfileManager), "加载所有插件配置失败: {ErrorMessage}", ex.Message);
        }

        return result;
    }

#endregion

#region Private Methods

    private Result SaveProfileToDisk(GameProfile profile)
    {
        var profileDirectory = GetProfileDirectory(profile.Id);
        Directory.CreateDirectory(profileDirectory);

        var filePath = Path.Combine(profileDirectory, AppConstants.ProfileFileName);
        return JsonHelper.SaveToFile(filePath, profile);
    }

    /// <summary>
    /// 加载所有已订阅的 Profile
    /// 只加载 SubscriptionManager 中已订阅的 Profile
    /// </summary>
    private void LoadAllProfiles()
    {
        // 确保 SubscriptionManager 已加载
        _subscriptionManager.Load();

        // 获取已订阅的 Profile 列表
        var subscribedProfiles = _subscriptionManager.GetSubscribedProfiles();

        // 如果没有订阅任何 Profile，确保默认 Profile 存在
        if (subscribedProfiles.Count == 0)
        {
            // 自动订阅默认 Profile
            EnsureDefaultProfileSubscribed();
            subscribedProfiles = _subscriptionManager.GetSubscribedProfiles();
        }

        // 只加载已订阅的 Profile
        foreach (var profileId in subscribedProfiles)
        {
            var profileDir = Path.Combine(ProfilesDirectory, profileId);
            var profilePath = Path.Combine(profileDir, AppConstants.ProfileFileName);

            if (!File.Exists(profilePath))
            {
                _logService.Warn(nameof(ProfileManager), "已订阅的 Profile 文件不存在: {ProfilePath}", profilePath);
                continue;
            }

            try
            {
                var profile = JsonHelper.LoadFromFile<GameProfile>(profilePath);
                if (profile.IsSuccess)
                {
                    Profiles.Add(profile.Value!);
                    _logService.Debug(nameof(ProfileManager), "已加载订阅的 Profile: {ProfileId}", profileId);
                }
            }
            catch (Exception ex)
            {
                _logService.Warn(nameof(ProfileManager), "加载 Profile 失败 [{ProfilePath}]: {ErrorMessage}",
                                 profilePath, ex.Message);
            }
        }

        // 确保默认 Profile 存在于列表中
        if (GetProfileById(AppConstants.DefaultProfileId) == null)
        {
            var defaultProfile = CreateDefaultProfile();
            Profiles.Add(defaultProfile);
        }
    }

    /// <summary>
    /// 确保默认 Profile 已订阅
    /// </summary>
    private void EnsureDefaultProfileSubscribed()
    {
        if (!_subscriptionManager.IsProfileSubscribed(AppConstants.DefaultProfileId))
        {
            // 检查内置模板是否存在
            if (_profileRegistry.ProfileExists(AppConstants.DefaultProfileId))
            {
                // 从内置模板订阅
                _subscriptionManager.SubscribeProfile(AppConstants.DefaultProfileId);
                _logService.Info(nameof(ProfileManager), "已自动订阅默认 Profile（从内置模板）");
            }
            else
            {
                // 内置模板不存在，创建默认 Profile 并手动添加到订阅
                CreateDefaultProfile();
                // 手动添加到订阅配置（因为没有内置模板）
                AddDefaultProfileToSubscription();
                _logService.Info(nameof(ProfileManager), "已创建并订阅默认 Profile");
            }
        }
    }

    /// <summary>
    /// 手动将默认 Profile 添加到订阅配置
    /// 用于内置模板不存在的情况
    /// </summary>
    private void AddDefaultProfileToSubscription()
    {
        // 直接操作订阅配置文件
        var subscriptionsPath = AppPaths.SubscriptionsFilePath;
        var config = new SubscriptionConfig();

        if (File.Exists(subscriptionsPath))
        {
            try
            {
                config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
            }
            catch
            {
                config = new SubscriptionConfig();
            }
        }

        if (!config.IsProfileSubscribed(AppConstants.DefaultProfileId))
        {
            config.AddProfile(AppConstants.DefaultProfileId);
            config.SaveToFile(subscriptionsPath);
        }

        // 重新加载 SubscriptionManager
        _subscriptionManager.Load();
    }

    /// <summary>
    /// 创建默认 Profile
    /// 优先从内置模板复制，否则创建新的
    /// </summary>
    private GameProfile CreateDefaultProfile()
    {
        var profileDir = GetProfileDirectory(AppConstants.DefaultProfileId);
        var profilePath = Path.Combine(profileDir, AppConstants.ProfileFileName);

        // 检查内置模板是否存在
        var templateDir = _profileRegistry.GetProfileTemplateDirectory(AppConstants.DefaultProfileId);
        var templatePath = Path.Combine(templateDir, AppConstants.ProfileFileName);

        if (File.Exists(templatePath))
        {
            // 从内置模板复制
            try
            {
                Directory.CreateDirectory(profileDir);
                CopyDirectory(templateDir, profileDir);

                var profile = JsonHelper.LoadFromFile<GameProfile>(profilePath);
                if (profile.IsSuccess)
                {
                    _logService.Info(nameof(ProfileManager), "已从内置模板创建默认 Profile");
                    return profile.Value!;
                }
            }
            catch (Exception ex)
            {
                _logService.Warn(nameof(ProfileManager), "从模板复制默认 Profile 失败: {ErrorMessage}", ex.Message);
            }
        }

        // 内置模板不存在或复制失败，创建新的默认 Profile
        var newProfile =
            new GameProfile { Id = AppConstants.DefaultProfileId, Name = AppConstants.DefaultProfileName, Icon = "🌐",
                              Version = 1,
                              Defaults = new ProfileDefaults { Url = AppConstants.DefaultHomeUrl,
                                                               SeekSeconds = AppConstants.DefaultSeekSeconds } };

        // 保存到文件
        SaveProfile(newProfile);
        _logService.Info(nameof(ProfileManager), "已创建新的默认 Profile");
        return newProfile;
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    /// <param name="sourceDir">源目录</param>
    /// <param name="targetDir">目标目录</param>
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(targetDir, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destDir = Path.Combine(targetDir, dirName);
            CopyDirectory(dir, destDir);
        }
    }

#endregion
}
}
