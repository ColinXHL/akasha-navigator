using System.Collections.Generic;
using System.Threading.Tasks;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.Core.Interfaces
{
public interface IProfileDeletionWorkflow
{
    DeleteProfilePlan PrepareDeletePlan(string profileId);

    Task ExecuteDeleteAsync(DeleteProfilePlan plan, IReadOnlyList<string> selectedPluginIds);
}
}
