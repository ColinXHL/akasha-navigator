using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Services;

/// <summary>
/// 合并旧版内置插件目录更新与远程插件包更新。
/// </summary>
public sealed class PluginUpdateService : IPluginUpdateService
{
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IUpdateManifestService _updateManifestService;
    private readonly IPluginPackageService _pluginPackageService;

    public PluginUpdateService(
        IPluginLibrary pluginLibrary,
        IUpdateManifestService updateManifestService,
        IPluginPackageService pluginPackageService)
    {
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _updateManifestService =
            updateManifestService ?? throw new ArgumentNullException(nameof(updateManifestService));
        _pluginPackageService =
            pluginPackageService ?? throw new ArgumentNullException(nameof(pluginPackageService));
    }

    public async Task<Result<IReadOnlyList<UpdateCheckResult>>> CheckAllUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        var manifestResult = await _updateManifestService.RefreshAsync(cancellationToken);
        if (manifestResult.IsFailure)
        {
            return Result<IReadOnlyList<UpdateCheckResult>>.Failure(manifestResult.Error!);
        }

        var installed = _pluginLibrary.GetInstalledPlugins()
            .ToDictionary(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase);
        var candidates = _pluginLibrary.CheckAllUpdates()
            .ToDictionary(update => update.PluginId, StringComparer.OrdinalIgnoreCase);

        foreach (var remote in _pluginPackageService.GetRemoteCatalog())
        {
            if (!installed.TryGetValue(remote.Id, out var installedPlugin) ||
                PluginLibrary.CompareVersions(remote.Version, installedPlugin.Version) <= 0)
            {
                continue;
            }

            if (candidates.TryGetValue(remote.Id, out var existing) &&
                PluginLibrary.CompareVersions(existing.AvailableVersion, remote.Version) >= 0)
            {
                continue;
            }

            candidates[remote.Id] = UpdateCheckResult.WithRemoteUpdate(
                remote.Id,
                installedPlugin.Version,
                remote.Version);
        }

        return Result<IReadOnlyList<UpdateCheckResult>>.Success(
            candidates.Values
                .OrderBy(update => update.PluginId, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public async Task<UpdateResult> UpdatePluginAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!update.HasUpdate || string.IsNullOrWhiteSpace(update.PluginId))
        {
            return UpdateResult.NoUpdateAvailable();
        }

        if (update.Source == PluginUpdateSource.BuiltIn)
        {
            return _pluginLibrary.UpdatePlugin(update.PluginId);
        }

        var oldVersion =
            _pluginLibrary.GetInstalledPluginInfo(update.PluginId)?.Version ?? update.CurrentVersion;
        var result = await _pluginPackageService.InstallOrUpdateAsync(
            update.PluginId,
            cancellationToken: cancellationToken);

        return result.IsSuccess
            ? UpdateResult.Success(oldVersion, result.Value!.Version)
            : UpdateResult.Failed(result.Error?.Message ?? "远程插件更新失败");
    }
}
