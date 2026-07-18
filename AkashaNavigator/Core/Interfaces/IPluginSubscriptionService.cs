using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 管理聚合仓库插件订阅，不负责 Profile 关联或删除插件数据。
/// </summary>
public interface IPluginSubscriptionService
{
    IReadOnlyList<PluginSubscriptionRecord> GetSubscriptions();

    PluginSubscriptionRecord? GetSubscription(string pluginId);

    bool IsSubscribed(string pluginId);

    IReadOnlyList<PluginSubscriptionUpdate> GetAvailableUpdates();

    Result<PluginSubscriptionRecord> Subscribe(
        string repositoryId,
        PluginRepositoryEntry entry);

    Result Unsubscribe(string pluginId);

    Result SetAutoUpdate(string pluginId, bool enabled);

    Result MarkInstalled(
        string pluginId,
        string installedVersion,
        string installedCommit);

    Result Reconcile(
        string repositoryId,
        PluginRepositorySnapshot snapshot);
}
