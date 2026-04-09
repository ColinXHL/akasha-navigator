using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.Services
{
public class ProfileDeletionWorkflow : IProfileDeletionWorkflow
{
    private readonly IProfileManager _profileManager;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;

    public ProfileDeletionWorkflow(IProfileManager profileManager,
                                   IPluginAssociationManager pluginAssociationManager,
                                   IPluginLibrary pluginLibrary,
                                   INotificationService notificationService,
                                   IEventBus eventBus)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _pluginAssociationManager = pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public DeleteProfilePlan PrepareDeletePlan(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return new DeleteProfilePlan();
        }

        var profile = _profileManager.GetProfileById(profileId);
        var profileName = profile?.Name ?? profileId;

        var uniquePluginChoices = _pluginAssociationManager
            .GetPluginsInProfile(profileId)
            .Where(p => _pluginAssociationManager.GetPluginReferenceCount(p.PluginId) == 1)
            .Select(p => p.PluginId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreatePluginChoice)
            .ToList();

        return new DeleteProfilePlan
        {
            ProfileId = profileId,
            ProfileName = profileName,
            RequiresPluginUninstallPrompt = uniquePluginChoices.Count > 0,
            PluginChoices = uniquePluginChoices
        };
    }

    public Task ExecuteDeleteAsync(DeleteProfilePlan plan, IReadOnlyList<string> selectedPluginIds)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.ProfileId))
        {
            return Task.CompletedTask;
        }

        var deleteResult = _profileManager.DeleteProfile(plan.ProfileId);
        if (!deleteResult.IsSuccess)
        {
            _notificationService.Error($"删除失败: {deleteResult.Error?.Message}");
            return Task.CompletedTask;
        }

        if (selectedPluginIds.Count > 0)
        {
            var successCount = 0;
            var failCount = 0;

            foreach (var pluginId in selectedPluginIds)
            {
                var result = _pluginLibrary.UninstallPlugin(pluginId);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            _eventBus.Publish(new PluginListChangedEvent());

            if (failCount > 0)
            {
                _notificationService.Warning($"Profile \"{plan.ProfileName}\" 已删除。插件卸载: 成功 {successCount} 个，失败 {failCount} 个");
            }
            else
            {
                _notificationService.Success($"Profile \"{plan.ProfileName}\" 已删除，同时卸载了 {successCount} 个插件");
            }
        }
        else
        {
            _notificationService.Success($"Profile \"{plan.ProfileName}\" 已删除");
        }

        _eventBus.Publish(new ProfileListChangedEvent());
        return Task.CompletedTask;
    }

    private PluginUninstallItem CreatePluginChoice(string pluginId)
    {
        var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
        return new PluginUninstallItem
        {
            PluginId = pluginId,
            Name = pluginInfo?.Name ?? pluginId,
            Description = pluginInfo?.Description ?? string.Empty,
            IsSelected = true
        };
    }
}
}
