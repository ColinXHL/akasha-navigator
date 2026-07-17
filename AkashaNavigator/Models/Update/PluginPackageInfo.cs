using System.Collections.Generic;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 远程插件包信息。
/// </summary>
public sealed class PluginPackageInfo
{
    public string FileName { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public List<DownloadSourceInfo> Sources { get; set; } = new();
}
