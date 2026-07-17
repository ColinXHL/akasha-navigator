using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 根据用户偏好和共享测速结果排列应用更新与插件下载源。
/// </summary>
public interface IDownloadSourceSelector
{
    Task<Result<IReadOnlyList<DownloadSourceInfo>>> GetOrderedSourcesAsync(
        PluginPackageInfo package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 立即测量给定下载源，并缓存为版本更新和插件下载共用的测速结果。
    /// </summary>
    Task<Result<IReadOnlyList<DownloadSourceMeasurement>>> MeasureSourcesAsync(
        PluginPackageInfo package,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    void ClearCache();
}
