using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Core.Interfaces;

public enum PluginPermissionConsentOperation
{
    Install,
    Update,
    FirstEnable
}

public interface IPluginPermissionConsentService
{
    bool EnsureHighRiskPermissionsApproved(
        PluginManifest manifest,
        PluginPermissionConsentOperation operation);

    bool RevokeHighRiskPermissionConsent(string pluginId);
}
