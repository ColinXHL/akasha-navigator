namespace AkashaNavigator.Models.PluginRepository;

/// <summary>
/// 聚合仓库插件订阅持久化状态。
/// </summary>
public sealed class PluginSubscriptionState
{
    public int SchemaVersion { get; set; } = 1;

    public List<PluginSubscriptionRecord> Subscriptions { get; set; } = new();
}

/// <summary>
/// 单个插件的仓库订阅记录。取消订阅不会删除安装目录。
/// </summary>
public sealed record PluginSubscriptionRecord
{
    public string PluginId { get; init; } = string.Empty;

    public string RepositoryId { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;

    public string LastKnownVersion { get; init; } = string.Empty;

    public string InstalledVersion { get; init; } = string.Empty;

    public string InstalledCommit { get; init; } = string.Empty;

    public bool AutoUpdate { get; init; } = true;

    public bool IsAvailable { get; init; } = true;
}

public sealed record PluginSubscriptionUpdate
{
    public string PluginId { get; init; } = string.Empty;

    public string InstalledVersion { get; init; } = string.Empty;

    public string AvailableVersion { get; init; } = string.Empty;

    public bool AutoUpdate { get; init; }
}
