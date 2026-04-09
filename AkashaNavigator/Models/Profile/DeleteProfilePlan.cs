using System;
using System.Collections.Generic;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Models.Profile
{
public class DeleteProfilePlan
{
    public string ProfileId { get; init; } = string.Empty;

    public string ProfileName { get; init; } = string.Empty;

    public bool RequiresPluginUninstallPrompt { get; init; }

    public IReadOnlyList<PluginUninstallItem> PluginChoices { get; init; } = Array.Empty<PluginUninstallItem>();
}
}
