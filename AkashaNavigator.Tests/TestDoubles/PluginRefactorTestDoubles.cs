using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;

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

    public Result DeleteResult { get; set; } = Result.Success();

    public string? LastDeletedProfileId { get; private set; }

    public FakeProfileManager(IEnumerable<GameProfile>? profiles = null)
    {
        if (profiles == null)
        {
            Profiles.Add(new GameProfile { Id = "profile-1", Name = "Profile 1" });
            return;
        }

        Profiles.AddRange(profiles);
        if (Profiles.Count > 0)
        {
            CurrentProfile = Profiles[0];
        }
    }

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

    public Result DeleteProfile(string id)
    {
        LastDeletedProfileId = id;
        return DeleteResult;
    }

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

public sealed class FakePluginAssociationManager : IPluginAssociationManager
{
    public event EventHandler<AssociationChangedEventArgs>? AssociationChanged;

    public event EventHandler<PluginEnabledChangedEventArgs>? PluginEnabledChanged;

    public string AssociationsFilePath => string.Empty;

    private readonly Dictionary<string, List<PluginReference>> _pluginsByProfile = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _profilesByPlugin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _originalPluginsByProfile = new(StringComparer.OrdinalIgnoreCase);

    public FakePluginAssociationManager(string profileId = "profile-1", IEnumerable<string>? uniquePluginIds = null)
    {
        if (uniquePluginIds == null)
        {
            return;
        }

        foreach (var pluginId in uniquePluginIds)
        {
            AddPluginToProfile(pluginId, profileId);
        }
    }

    public void SetPluginsForProfile(string profileId, IEnumerable<string> pluginIds)
    {
        RemoveProfile(profileId);
        foreach (var pluginId in pluginIds)
        {
            AddPluginToProfile(pluginId, profileId);
        }
    }

    public void ReloadIndex() { }

    public List<string> GetProfilesUsingPlugin(string pluginId)
    {
        return _profilesByPlugin.TryGetValue(pluginId, out var profileIds)
            ? profileIds.ToList()
            : new List<string>();
    }

    public int GetPluginReferenceCount(string pluginId)
    {
        return _profilesByPlugin.TryGetValue(pluginId, out var profileIds)
            ? profileIds.Count
            : 0;
    }

    public List<PluginReference> GetPluginsInProfile(string profileId)
    {
        return _pluginsByProfile.TryGetValue(profileId, out var plugins)
            ? plugins.Select(p => new PluginReference(p.PluginId, p.Enabled)).ToList()
            : new List<PluginReference>();
    }

    public List<string> GetMissingPlugins(string profileId)
    {
        return new List<string>();
    }

    public bool ProfileContainsPlugin(string profileId, string pluginId)
    {
        return _pluginsByProfile.TryGetValue(profileId, out var plugins) &&
               plugins.Any(p => p.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public bool AddPluginToProfile(string pluginId, string profileId, bool enabled = true)
    {
        if (!_pluginsByProfile.TryGetValue(profileId, out var plugins))
        {
            plugins = new List<PluginReference>();
            _pluginsByProfile[profileId] = plugins;
        }

        if (plugins.Any(p => p.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        plugins.Add(new PluginReference(pluginId, enabled));

        if (!_profilesByPlugin.TryGetValue(pluginId, out var profileIds))
        {
            profileIds = new List<string>();
            _profilesByPlugin[pluginId] = profileIds;
        }

        if (!profileIds.Contains(profileId, StringComparer.OrdinalIgnoreCase))
        {
            profileIds.Add(profileId);
        }

        AssociationChanged?.Invoke(this, new AssociationChangedEventArgs(AssociationChangeType.Added, pluginId, profileId));
        return true;
    }

    public int AddPluginsToProfile(IEnumerable<string> pluginIds, string profileId)
    {
        var count = 0;
        foreach (var pluginId in pluginIds)
        {
            if (AddPluginToProfile(pluginId, profileId))
            {
                count++;
            }
        }

        return count;
    }

    public int AddPluginToProfiles(string pluginId, IEnumerable<string> profileIds)
    {
        return profileIds.Count(profileId => AddPluginToProfile(pluginId, profileId));
    }

    public bool RemovePluginFromProfile(string pluginId, string profileId)
    {
        if (!_pluginsByProfile.TryGetValue(profileId, out var plugins))
        {
            return false;
        }

        var removed = plugins.RemoveAll(p => p.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return false;
        }

        if (_profilesByPlugin.TryGetValue(pluginId, out var profileIds))
        {
            profileIds.RemoveAll(id => id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
            if (profileIds.Count == 0)
            {
                _profilesByPlugin.Remove(pluginId);
            }
        }

        AssociationChanged?.Invoke(this, new AssociationChangedEventArgs(AssociationChangeType.Removed, pluginId, profileId));
        return true;
    }

    public int RemovePluginFromAllProfiles(string pluginId)
    {
        if (!_profilesByPlugin.TryGetValue(pluginId, out var profileIds))
        {
            return 0;
        }

        var removedCount = 0;
        foreach (var profileId in profileIds.ToList())
        {
            if (RemovePluginFromProfile(pluginId, profileId))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    public bool RemoveProfile(string profileId)
    {
        if (!_pluginsByProfile.TryGetValue(profileId, out var plugins))
        {
            return false;
        }

        foreach (var reference in plugins)
        {
            if (_profilesByPlugin.TryGetValue(reference.PluginId, out var profileIds))
            {
                profileIds.RemoveAll(id => id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
                if (profileIds.Count == 0)
                {
                    _profilesByPlugin.Remove(reference.PluginId);
                }
            }
        }

        _pluginsByProfile.Remove(profileId);
        _originalPluginsByProfile.Remove(profileId);
        return true;
    }

    public bool SetPluginEnabled(string profileId, string pluginId, bool enabled)
    {
        if (!_pluginsByProfile.TryGetValue(profileId, out var plugins))
        {
            return false;
        }

        var plugin = plugins.FirstOrDefault(p => p.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin == null)
        {
            return false;
        }

        plugin.Enabled = enabled;
        PluginEnabledChanged?.Invoke(this, new PluginEnabledChangedEventArgs(profileId, pluginId, enabled));
        return true;
    }

    public bool? GetPluginEnabled(string profileId, string pluginId)
    {
        if (!_pluginsByProfile.TryGetValue(profileId, out var plugins))
        {
            return null;
        }

        var plugin = plugins.FirstOrDefault(p => p.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
        return plugin?.Enabled;
    }

    public void SetOriginalPlugins(string profileId, List<string> pluginIds)
    {
        _originalPluginsByProfile[profileId] = pluginIds.ToList();
    }

    public List<string> GetOriginalPlugins(string profileId)
    {
        return _originalPluginsByProfile.TryGetValue(profileId, out var pluginIds)
            ? pluginIds.ToList()
            : new List<string>();
    }

    public bool HasOriginalPlugins(string profileId)
    {
        return _originalPluginsByProfile.TryGetValue(profileId, out var pluginIds) && pluginIds.Count > 0;
    }

    public List<string> GetMissingOriginalPlugins(string profileId)
    {
        var originals = GetOriginalPlugins(profileId);
        var current = GetPluginsInProfile(profileId)
            .Select(p => p.PluginId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return originals.Where(pluginId => !current.Contains(pluginId)).ToList();
    }

    public void RemoveOriginalPlugins(string profileId)
    {
        _originalPluginsByProfile.Remove(profileId);
    }

    public void ApplyPluginPresetConfigs(string profileId, Dictionary<string, Dictionary<string, JsonElement>>? presetConfigs)
    {
    }

    public List<string> GetAllProfileIds()
    {
        return _pluginsByProfile.Keys.ToList();
    }
}

public sealed class FakePluginLibrary : IPluginLibrary
{
    public event EventHandler<PluginLibraryChangedEventArgs> PluginChanged
    {
        add { }
        remove { }
    }

    public string LibraryDirectory => string.Empty;

    public string LibraryIndexPath => string.Empty;

    public Dictionary<string, InstalledPluginInfo> InstalledPlugins { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> UninstalledPluginIds { get; } = new();

    public void ReloadIndex() { }

    public List<InstalledPluginInfo> GetInstalledPlugins() => InstalledPlugins.Values.ToList();

    public bool IsInstalled(string pluginId) => InstalledPlugins.ContainsKey(pluginId);

    public string GetPluginDirectory(string pluginId) => string.Empty;

    public PluginManifest? GetPluginManifest(string pluginId) => null;

    public InstalledPluginInfo? GetInstalledPluginInfo(string pluginId)
    {
        return InstalledPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    public Result<InstalledPluginInfo> InstallPlugin(string pluginId, string? sourceDirectory = null)
    {
        var plugin = new InstalledPluginInfo
        {
            Id = pluginId,
            Name = pluginId,
            Version = "1.0.0"
        };

        InstalledPlugins[pluginId] = plugin;
        return Result<InstalledPluginInfo>.Success(plugin);
    }

    public Result UninstallPlugin(string pluginId, bool force = false, Func<string, List<string>>? getReferencingProfiles = null)
    {
        InstalledPlugins.Remove(pluginId);
        UninstalledPluginIds.Add(pluginId);
        return Result.Success();
    }

    public UpdateCheckResult CheckForUpdate(string pluginId) => UpdateCheckResult.NoUpdate(pluginId, "1.0.0");

    public List<UpdateCheckResult> CheckAllUpdates() => new();

    public UpdateResult UpdatePlugin(string pluginId) => UpdateResult.NoUpdateAvailable();
}

public sealed class RecordingEventBus : IEventBus
{
    public List<object> PublishedEvents { get; } = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        PublishedEvents.Add(@event);
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
    }

    public void Clear()
    {
        PublishedEvents.Clear();
    }
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
    public bool ConfirmResponse { get; set; } = true;

    public List<string> InfoMessages { get; } = new();

    public List<string> SuccessMessages { get; } = new();

    public List<string> WarningMessages { get; } = new();

    public List<string> ErrorMessages { get; } = new();

    public void Show(string message, NotificationType type = NotificationType.Info, string? title = null, int durationMs = 3000) { }

    public Task<bool> ConfirmAsync(string message, string? title = null) => Task.FromResult(ConfirmResponse);

    public Task<bool?> ShowDialogAsync(string message, string? title = null, string yesText = "确定", string noText = "取消", bool showCancel = false)
    {
        return Task.FromResult<bool?>(true);
    }

    public void Info(string message, string? title = null, int durationMs = 3000)
    {
        InfoMessages.Add(message);
    }

    public void Success(string message, string? title = null, int durationMs = 3000)
    {
        SuccessMessages.Add(message);
    }

    public void Warning(string message, string? title = null, int durationMs = 3000)
    {
        WarningMessages.Add(message);
    }

    public void Error(string message, string? title = null, int durationMs = 4000)
    {
        ErrorMessages.Add(message);
    }
}
