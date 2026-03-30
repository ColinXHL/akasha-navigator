using System.Threading.Tasks;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Core.Interfaces;

public interface IAppUpdateService
{
    Task<Result<AppUpdateCheckResult>> CheckForUpdateAsync(bool includePrerelease);

    Result StartUpdater(string sourceId = "cnb");
}
