using System;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 下载并校验 catalog Release 插件包。
/// </summary>
public interface IPluginPackageService
{
    Task<Result<DownloadedPluginPackage>> DownloadPackageAsync(
        string pluginId,
        PluginPackageInfo package,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
