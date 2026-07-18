using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Services;

/// <summary>
/// 以独立文件保存聚合仓库插件订阅。
/// </summary>
public sealed class PluginSubscriptionService : IPluginSubscriptionService
{
    private readonly ILogService _logService;
    private readonly IPluginLibrary? _pluginLibrary;
    private readonly string _stateFilePath;
    private readonly object _stateLock = new();
    private PluginSubscriptionState _state;

    public PluginSubscriptionService(
        ILogService logService,
        IPluginLibrary pluginLibrary)
        : this(
            logService,
            AppPaths.PluginRepositorySubscriptionsFilePath,
            pluginLibrary)
    {
    }

    internal PluginSubscriptionService(
        ILogService logService,
        string stateFilePath)
        : this(logService, stateFilePath, null)
    {
    }

    private PluginSubscriptionService(
        ILogService logService,
        string stateFilePath,
        IPluginLibrary? pluginLibrary)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _pluginLibrary = pluginLibrary;
        _stateFilePath = stateFilePath ?? throw new ArgumentNullException(nameof(stateFilePath));
        _state = LoadState();
    }

    public IReadOnlyList<PluginSubscriptionRecord> GetSubscriptions()
    {
        lock (_stateLock)
        {
            return _state.Subscriptions
                .Select(Clone)
                .OrderBy(record => record.PluginId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public PluginSubscriptionRecord? GetSubscription(string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return null;
        }

        lock (_stateLock)
        {
            var record = Find(pluginId);
            return record == null ? null : Clone(record);
        }
    }

    public bool IsSubscribed(string pluginId)
    {
        return GetSubscription(pluginId) != null;
    }

    public IReadOnlyList<PluginSubscriptionUpdate> GetAvailableUpdates()
    {
        lock (_stateLock)
        {
            var installed = _pluginLibrary?
                .GetInstalledPlugins()
                .ToDictionary(
                    plugin => plugin.Id,
                    StringComparer.OrdinalIgnoreCase);
            return _state.Subscriptions
                .Select(
                    record => new {
                        Record = record,
                        InstalledVersion =
                            installed?.GetValueOrDefault(record.PluginId)?.Version ??
                            record.InstalledVersion
                    })
                .Where(
                    item =>
                        item.Record.IsAvailable &&
                        !string.IsNullOrWhiteSpace(item.InstalledVersion) &&
                        PluginLibrary.CompareVersions(
                            item.Record.LastKnownVersion,
                            item.InstalledVersion) > 0)
                .Select(
                    item => new PluginSubscriptionUpdate {
                        PluginId = item.Record.PluginId,
                        InstalledVersion = item.InstalledVersion,
                        AvailableVersion = item.Record.LastKnownVersion,
                        AutoUpdate = item.Record.AutoUpdate
                    })
                .OrderBy(update => update.PluginId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public Result<PluginSubscriptionRecord> Subscribe(
        string repositoryId,
        PluginRepositoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var validation = ValidateSubscription(repositoryId, entry);
        if (validation != null)
        {
            return Result<PluginSubscriptionRecord>.Failure(validation);
        }

        lock (_stateLock)
        {
            var existing = Find(entry.Id);
            if (existing != null &&
                (!string.Equals(
                     existing.RepositoryId,
                     repositoryId,
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     existing.RepositoryPath,
                     entry.Path,
                     StringComparison.Ordinal)))
            {
                return Result<PluginSubscriptionRecord>.Failure(
                    Error.BusinessLogic(
                        "PLUGIN_SUBSCRIPTION_SOURCE_CONFLICT",
                        $"插件 {entry.Id} 已订阅到其他仓库来源"));
            }

            var updated = new PluginSubscriptionRecord {
                PluginId = entry.Id,
                RepositoryId = repositoryId,
                RepositoryPath = entry.Path,
                LastKnownVersion = entry.Version,
                InstalledVersion = existing?.InstalledVersion ?? string.Empty,
                InstalledCommit = existing?.InstalledCommit ?? string.Empty,
                AutoUpdate = existing?.AutoUpdate ?? true,
                IsAvailable = true
            };
            var previousState = CloneState(_state);
            if (existing == null)
            {
                _state.Subscriptions.Add(updated);
            }
            else
            {
                _state.Subscriptions[_state.Subscriptions.IndexOf(existing)] = updated;
            }

            var saveResult = SaveState();
            if (saveResult.IsFailure)
            {
                _state = previousState;
                return Result<PluginSubscriptionRecord>.Failure(saveResult.Error!);
            }

            return Result<PluginSubscriptionRecord>.Success(Clone(updated));
        }
    }

    public Result Unsubscribe(string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result.Failure(
                Error.Validation(
                    "PLUGIN_SUBSCRIPTION_ID_INVALID",
                    "插件订阅 ID 无效"));
        }

        lock (_stateLock)
        {
            var existing = Find(pluginId);
            if (existing == null)
            {
                return Result.Success();
            }

            var previousState = CloneState(_state);
            _state.Subscriptions.Remove(existing);
            var saveResult = SaveState();
            if (saveResult.IsFailure)
            {
                _state = previousState;
                return saveResult;
            }

            return Result.Success();
        }
    }

    public Result SetAutoUpdate(string pluginId, bool enabled)
    {
        return UpdateSubscription(
            pluginId,
            record => record with { AutoUpdate = enabled });
    }

    public Result MarkInstalled(
        string pluginId,
        string installedVersion,
        string installedCommit)
    {
        if (string.IsNullOrWhiteSpace(installedVersion) ||
            string.IsNullOrWhiteSpace(installedCommit))
        {
            return Result.Failure(
                Error.Validation(
                    "PLUGIN_SUBSCRIPTION_INSTALL_STATE_INVALID",
                    "插件安装版本和 catalog commit 不能为空"));
        }

        return UpdateSubscription(
            pluginId,
            record => record with {
                InstalledVersion = installedVersion,
                InstalledCommit = installedCommit
            });
    }

    public Result Reconcile(
        string repositoryId,
        PluginRepositorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            return Result.Failure(
                Error.Validation(
                    "PLUGIN_SUBSCRIPTION_REPOSITORY_INVALID",
                    "插件仓库 ID 不能为空"));
        }

        lock (_stateLock)
        {
            var catalog = snapshot.Index.Plugins.ToDictionary(
                entry => entry.Id,
                StringComparer.OrdinalIgnoreCase);
            var previousState = CloneState(_state);
            var changed = false;
            for (var index = 0; index < _state.Subscriptions.Count; index++)
            {
                var record = _state.Subscriptions[index];
                if (!string.Equals(
                        record.RepositoryId,
                        repositoryId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var available = catalog.TryGetValue(record.PluginId, out var entry) &&
                                string.Equals(
                                    entry.Path,
                                    record.RepositoryPath,
                                    StringComparison.Ordinal);
                var updated = record with {
                    IsAvailable = available,
                    LastKnownVersion = available
                        ? entry!.Version
                        : record.LastKnownVersion
                };
                if (updated != record)
                {
                    _state.Subscriptions[index] = updated;
                    changed = true;
                }
            }

            if (!changed)
            {
                return Result.Success();
            }

            var saveResult = SaveState();
            if (saveResult.IsFailure)
            {
                _state = previousState;
            }

            return saveResult;
        }
    }

    private PluginSubscriptionState LoadState()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new PluginSubscriptionState();
        }

        var result = JsonHelper.LoadFromFile<PluginSubscriptionState>(_stateFilePath);
        if (result.IsFailure ||
            result.Value!.SchemaVersion != 1 ||
            result.Value.Subscriptions == null ||
            result.Value.Subscriptions.Any(record => !IsValid(record)))
        {
            _logService.Warn(
                nameof(PluginSubscriptionService),
                "插件仓库订阅文件无效，将使用空状态");
            return new PluginSubscriptionState();
        }

        var unique = result.Value.Subscriptions
            .GroupBy(record => record.PluginId, StringComparer.OrdinalIgnoreCase)
            .Select(group => Clone(group.First()))
            .ToList();
        return new PluginSubscriptionState {
            SchemaVersion = 1,
            Subscriptions = unique
        };
    }

    private Result SaveState()
    {
        var temporaryPath =
            $"{_stateFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var saveResult = JsonHelper.SaveToFile(temporaryPath, _state);
            if (saveResult.IsFailure)
            {
                TryDeleteFile(temporaryPath);
                return saveResult;
            }

            File.Move(temporaryPath, _stateFilePath, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex)
        {
            TryDeleteFile(temporaryPath);
            return Result.Failure(
                Error.FileSystem(
                    "PLUGIN_SUBSCRIPTION_SAVE_FAILED",
                    $"保存插件订阅失败: {ex.Message}",
                    ex,
                    _stateFilePath));
        }
    }

    private PluginSubscriptionRecord? Find(string pluginId)
    {
        return _state.Subscriptions.FirstOrDefault(
            record => string.Equals(
                record.PluginId,
                pluginId,
                StringComparison.OrdinalIgnoreCase));
    }

    private Result UpdateSubscription(
        string pluginId,
        Func<PluginSubscriptionRecord, PluginSubscriptionRecord> update)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result.Failure(
                Error.Validation(
                    "PLUGIN_SUBSCRIPTION_ID_INVALID",
                    "插件订阅 ID 无效"));
        }

        lock (_stateLock)
        {
            var existing = Find(pluginId);
            if (existing == null)
            {
                return Result.Failure(
                    Error.BusinessLogic(
                        "PLUGIN_SUBSCRIPTION_NOT_FOUND",
                        $"插件 {pluginId} 尚未订阅"));
            }

            var previousState = CloneState(_state);
            _state.Subscriptions[_state.Subscriptions.IndexOf(existing)] =
                update(existing);
            var saveResult = SaveState();
            if (saveResult.IsFailure)
            {
                _state = previousState;
            }

            return saveResult;
        }
    }

    private static Error? ValidateSubscription(
        string repositoryId,
        PluginRepositoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            return Error.Validation(
                "PLUGIN_SUBSCRIPTION_REPOSITORY_INVALID",
                "插件仓库 ID 不能为空");
        }

        if (!PluginIdValidator.IsValid(entry.Id) ||
            !string.Equals(
                entry.Path,
                $"{AppConstants.PluginsDirectoryName}/{entry.Id}",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(entry.Version))
        {
            return Error.Validation(
                "PLUGIN_SUBSCRIPTION_ENTRY_INVALID",
                "插件仓库条目无效");
        }

        return null;
    }

    private static bool IsValid(PluginSubscriptionRecord? record)
    {
        return record != null &&
               !string.IsNullOrWhiteSpace(record.RepositoryId) &&
               PluginIdValidator.IsValid(record.PluginId) &&
               string.Equals(
                   record.RepositoryPath,
                   $"{AppConstants.PluginsDirectoryName}/{record.PluginId}",
                   StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(record.LastKnownVersion) &&
               (string.IsNullOrWhiteSpace(record.InstalledVersion)
                   ? string.IsNullOrWhiteSpace(record.InstalledCommit)
                   : !string.IsNullOrWhiteSpace(record.InstalledCommit));
    }

    private static PluginSubscriptionRecord Clone(
        PluginSubscriptionRecord record)
    {
        return record with { };
    }

    private static PluginSubscriptionState CloneState(
        PluginSubscriptionState state)
    {
        return new PluginSubscriptionState {
            SchemaVersion = state.SchemaVersion,
            Subscriptions = state.Subscriptions.Select(Clone).ToList()
        };
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 临时文件由系统后续清理。
        }
    }
}
