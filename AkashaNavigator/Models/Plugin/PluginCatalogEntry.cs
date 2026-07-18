using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Models.Plugin;

/// <summary>
/// 合并内置与远程来源后的插件目录条目。
/// </summary>
public sealed class PluginCatalogEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Author { get; init; }

    /// <summary>
    /// 内置插件本地目录；远程插件保持为 null。
    /// </summary>
    public string? LocalSourceDirectory { get; init; }

    public bool IsRemote { get; init; }

    public string DistributionType { get; init; } = string.Empty;

    public string? MinHostVersion { get; init; }

    public PluginPackageInfo? Package { get; init; }
}
