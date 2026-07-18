using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 管理官方插件仓库只读缓存和 repo.json。
/// </summary>
public interface IPluginRepositoryService
{
    string RepositoryDirectory { get; }

    PluginRepositorySnapshot? Current { get; }

    PluginRepositorySettings Settings { get; }

    Task<Result<PluginRepositorySnapshot>> InitializeAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PluginRepositorySnapshot>> RefreshAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PluginRepositorySnapshot>> ResetAsync(
        CancellationToken cancellationToken = default);

    Result SaveSettings(PluginRepositorySettings settings);
}
