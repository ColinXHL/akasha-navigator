using System.Collections.Generic;

namespace AkashaNavigator.Models.PluginRepository;

/// <summary>
/// catalog 分支根目录 repo.json。
/// </summary>
public sealed class PluginRepositoryIndex
{
    public int SchemaVersion { get; set; }

    /// <summary>
    /// 生成 catalog 的 main 源提交。
    /// </summary>
    public string Commit { get; set; } = string.Empty;

    public List<PluginRepositoryEntry> Plugins { get; set; } = new();
}

/// <summary>
/// repo.json 中的插件摘要。
/// </summary>
public sealed class PluginRepositoryEntry
{
    public string Id { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DistributionType { get; set; } = string.Empty;

    public bool HasBackend { get; set; }

    public string MinHostVersion { get; set; } = string.Empty;
}

/// <summary>
/// 一次仓库读取或同步后的不可变快照。
/// </summary>
public sealed record PluginRepositorySnapshot(
    PluginRepositoryIndex Index,
    string CatalogCommit,
    bool UsedCache);
