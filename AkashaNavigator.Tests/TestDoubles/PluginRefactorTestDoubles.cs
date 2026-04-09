using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Plugins.Core;

namespace AkashaNavigator.Tests.TestDoubles;

public sealed class FakeProfileManager : IProfileManager
{
    public event EventHandler<GameProfile>? ProfileChanged;

    public GameProfile CurrentProfile { get; private set; } = new() { Id = "profile-1", Name = "Profile 1" };

    public List<GameProfile> Profiles { get; } = new();

    public IReadOnlyList<GameProfile> InstalledProfiles => Profiles;

    public string DataDirectory => string.Empty;

    public string ProfilesDirectory => string.Empty;

    public string[] ProfileIcons => Array.Empty<string>();

    public bool SwitchProfile(string profileId)
    {
        CurrentProfile = new GameProfile { Id = profileId, Name = profileId };
        ProfileChanged?.Invoke(this, CurrentProfile);
        return true;
    }

    public GameProfile? GetProfileById(string id) => Profiles.Find(p => p.Id == id);

    public string GetCurrentProfileDirectory() => string.Empty;

    public string GetProfileDirectory(string profileId) => string.Empty;

    public void SaveCurrentProfile() { }

    public void SaveProfile(GameProfile profile) { }

    public UnsubscribeResult UnsubscribeProfile(string profileId) => UnsubscribeResult.Succeeded();

    public void ReloadProfiles() { }

    public Result<string> CreateProfile(string? id, string name, string icon, List<string>? pluginIds) => Result<string>.Success(id ?? "new-profile");

    public bool UpdateProfile(string id, string newName, string newIcon) => true;

    public bool UpdateProfile(string id, ProfileUpdateData updateData) => true;

    public Result DeleteProfile(string id) => Result.Success();

    public bool IsDefaultProfile(string id) => false;

    public bool ProfileIdExists(string id) => false;

    public string GenerateProfileId(string name) => name;

    public bool SubscribeProfile(string profileId) => true;

    public UnsubscribeResult UnsubscribeProfileViaSubscription(string profileId) => UnsubscribeResult.Succeeded();

    public ProfileExportData? ExportProfile(string profileId) => null;

    public bool ExportProfileToFile(string profileId, string filePath) => true;

    public ProfileImportResult ImportProfile(ProfileExportData data, bool overwrite = false) => ProfileImportResult.Success(data.ProfileId);

    public ProfileImportResult ImportProfileFromFile(string filePath, bool overwrite = false) => ProfileImportResult.Success("profile-1");

    public ProfileImportResult PreviewImport(ProfileExportData data) => ProfileImportResult.Success(data.ProfileId);

    public List<PluginReference> GetPluginReferences(string profileId) => new();

    public bool SetPluginEnabled(string profileId, string pluginId, bool enabled) => true;

    public Dictionary<string, object>? GetPluginConfig(string profileId, string pluginId) => new();

    public bool SavePluginConfig(string profileId, string pluginId, Dictionary<string, object> config) => true;

    public bool DeletePluginConfig(string profileId, string pluginId) => true;

    public string GetPluginConfigsDirectory(string profileId) => string.Empty;

    public Dictionary<string, Dictionary<string, object>> GetAllPluginConfigs(string profileId) => new();
}

public sealed class FakeLogService : ILogService
{
    public string LogDirectory => string.Empty;

    public void Debug(string source, string message) { }

    public void Debug(string source, string template, params object?[] args) { }

    public void Info(string source, string message) { }

    public void Info(string source, string template, params object?[] args) { }

    public void Warn(string source, string message) { }

    public void Warn(string source, string template, params object?[] args) { }

    public void Error(string source, string message) { }

    public void Error(string source, string template, params object?[] args) { }

    public void Error(string source, Exception ex, string template, params object?[] args) { }
}

public sealed class FakePluginHost : IPluginHost
{
    public event EventHandler<PluginContext>? PluginLoaded;
    public event EventHandler<string>? PluginUnloaded;

    public IReadOnlyList<PluginContext> LoadedPlugins { get; } = Array.Empty<PluginContext>();

    public string? CurrentProfileId { get; private set; }

    public string? ReloadedPluginId { get; private set; }

    public void LoadPluginsForProfile(string profileId)
    {
        CurrentProfileId = profileId;
    }

    public void UnloadAllPlugins() { }

    public void SetPluginEnabled(string pluginId, bool enabled) { }

    public PluginContext? GetPlugin(string pluginId) => null;

    public PluginConfig? GetPluginConfig(string pluginId) => null;

    public void SavePluginConfig(string pluginId) { }

    public void BroadcastEvent(string eventName, object data) { }

    public void BroadcastPlayStateChanged(bool playing) { }

    public void BroadcastTimeUpdate(double currentTime, double duration) { }

    public void BroadcastUrlChanged(string url) { }

    public void EnablePlugin(string profileId, string pluginId) { }

    public void ReloadPlugin(string pluginId)
    {
        ReloadedPluginId = pluginId;
    }

    public string GetPluginConfigDirectory(string profileId, string pluginId) => string.Empty;

    public void Dispose() { }
}

public sealed class FakeNotificationService : INotificationService
{
    public void Show(string message, NotificationType type = NotificationType.Info, string? title = null, int durationMs = 3000) { }

    public Task<bool> ConfirmAsync(string message, string? title = null) => Task.FromResult(true);

    public Task<bool?> ShowDialogAsync(string message, string? title = null, string yesText = "确定", string noText = "取消", bool showCancel = false)
    {
        return Task.FromResult<bool?>(true);
    }

    public void Info(string message, string? title = null, int durationMs = 3000) { }

    public void Success(string message, string? title = null, int durationMs = 3000) { }

    public void Warning(string message, string? title = null, int durationMs = 3000) { }

    public void Error(string message, string? title = null, int durationMs = 4000) { }
}
