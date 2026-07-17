namespace AkashaNavigator.Models.Update;

/// <summary>
/// 可独立于插件包更新的资源信息。
/// </summary>
public sealed class PluginResourceInfo
{
    public string Version { get; set; } = string.Empty;

    public string UpstreamRelease { get; set; } = string.Empty;

    public string MinPluginVersion { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public int EntryCount { get; set; }

    public string Url { get; set; } = string.Empty;
}
