using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 根据用户偏好和实际网络速度排列插件下载源。
/// </summary>
public interface IDownloadSourceSelector
{
    Task<Result<IReadOnlyList<DownloadSourceInfo>>> GetOrderedSourcesAsync(
        PluginPackageInfo package,
        CancellationToken cancellationToken = default);

    void ClearCache();
}
