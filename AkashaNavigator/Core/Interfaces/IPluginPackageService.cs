using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 远程插件目录、版本检查、包下载和安装服务。
/// </summary>
public interface IPluginPackageService
{
    IReadOnlyList<PluginCatalogEntry> GetRemoteCatalog();

    bool IsUpdateAvailable(string pluginId);

    Task<Result<InstalledPluginInfo>> InstallOrUpdateAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
