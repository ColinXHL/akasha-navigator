using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 统一处理聚合仓库和本地 ZIP 插件安装。
/// </summary>
public interface IPluginInstaller
{
    Task<Result<InstalledPluginInfo>> InstallOrUpdateRepositoryPluginAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Result<InstalledPluginInfo> InstallOrUpdateRepositoryPlugin(
        string pluginId);

    Result<InstalledPluginInfo> InstallPackage(string archivePath);
}
