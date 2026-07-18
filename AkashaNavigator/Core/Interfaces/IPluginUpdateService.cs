using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 合并内置目录与远程插件包的统一更新服务。
/// </summary>
public interface IPluginUpdateService
{
    /// <summary>
    /// 刷新远程清单并检查所有已安装插件的可用更新。
    /// </summary>
    Task<Result<IReadOnlyList<UpdateCheckResult>>> CheckAllUpdatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按检查结果中记录的来源安装指定更新。
    /// </summary>
    Task<UpdateResult> UpdatePluginAsync(
        UpdateCheckResult update,
        CancellationToken cancellationToken = default);
}
