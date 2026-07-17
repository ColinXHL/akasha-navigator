using System.Collections.Generic;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 远程插件条目。
/// </summary>
public sealed class RemotePluginInfo
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string MinHostVersion { get; set; } = string.Empty;

    public PluginPackageInfo? Package { get; set; }

    public Dictionary<string, PluginResourceInfo> Resources { get; set; } = new();
}
