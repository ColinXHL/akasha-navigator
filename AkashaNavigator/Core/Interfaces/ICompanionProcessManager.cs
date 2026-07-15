using System.Text.Json;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Core.Interfaces;

public interface ICompanionProcessManager : IDisposable
{
    Task<CompanionStatus> StartAsync(
        string pluginId,
        string pluginDirectory,
        CompanionManifest manifest,
        CancellationToken cancellationToken = default);

    Task<JsonElement?> InvokeAsync(
        string pluginId,
        string method,
        JsonElement? payload,
        CancellationToken cancellationToken = default);

    CompanionStatus GetStatus(string pluginId);

    Task StopAsync(string pluginId, CancellationToken cancellationToken = default);

    Task StopAllAsync(CancellationToken cancellationToken = default);
}
