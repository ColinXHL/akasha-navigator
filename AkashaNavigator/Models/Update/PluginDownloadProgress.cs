namespace AkashaNavigator.Models.Update;

/// <summary>
/// 远程插件包下载进度。
/// </summary>
public sealed class PluginDownloadProgress
{
    public string SourceId { get; init; } = string.Empty;

    public long BytesReceived { get; init; }

    public long TotalBytes { get; init; }

    public double Percentage =>
        TotalBytes <= 0 ? 0 : BytesReceived * 100d / TotalBytes;
}
