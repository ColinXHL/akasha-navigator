using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 下载、缓存并提供应用与插件共用的更新清单。
/// </summary>
public interface IUpdateManifestService
{
    /// <summary>
    /// 最近一次成功读取的清单；无可用缓存时为 null。
    /// </summary>
    UpdateManifest? Current { get; }

    /// <summary>
    /// 使用 ETag 或 Last-Modified 执行条件请求并返回最新可用清单。
    /// 网络请求失败但本地缓存可用时返回缓存。
    /// </summary>
    Task<Result<UpdateManifest>> RefreshAsync(CancellationToken cancellationToken = default);
}
