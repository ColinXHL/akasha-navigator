using System.Windows;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginSettingsWindowService
{
    void Show(string pluginId, string pluginName, string pluginDirectory, string configDirectory, Window? owner = null,
              string? profileId = null);
}
