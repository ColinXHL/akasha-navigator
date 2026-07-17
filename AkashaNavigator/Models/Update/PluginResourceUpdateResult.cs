namespace AkashaNavigator.Models.Update;

/// <summary>
/// 插件独立资源更新状态。
/// </summary>
public enum PluginResourceUpdateStatus
{
    ManifestUnavailable,
    ResourceUnavailable,
    PluginNotInstalled,
    PluginVersionTooLow,
    UpToDate,
    Updated
}

/// <summary>
/// 单个插件独立资源的更新结果。
/// </summary>
public sealed record PluginResourceUpdateResult(
    PluginResourceUpdateStatus Status,
    string? FilePath = null,
    bool TakesEffectOnNextWorkerStart = false);

/// <summary>
/// 已保存的插件独立资源状态。
/// </summary>
public sealed class PluginResourceState
{
    public string Version { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public int EntryCount { get; set; }
}
