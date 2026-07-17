using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 更新可独立于插件包发布的用户资源。
/// </summary>
public interface IPluginResourceUpdateService
{
    /// <summary>
    /// 在 Akasha Automation 已安装且版本满足要求时更新拾取黑名单。
    /// </summary>
    Task<Result<PluginResourceUpdateResult>> UpdatePickBlacklistAsync(
        CancellationToken cancellationToken = default);
}
