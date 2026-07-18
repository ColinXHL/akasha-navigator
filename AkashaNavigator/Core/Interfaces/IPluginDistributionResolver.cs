using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginDistributionResolver
{
    Task<Result<ResolvedPluginDistribution>> ResolveAsync(
        string pluginId,
        PluginRepositoryEntry entry,
        CatalogPluginManifest manifest,
        string repositorySourceDirectory,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
