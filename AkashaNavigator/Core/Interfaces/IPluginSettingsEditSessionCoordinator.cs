using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginSettingsEditSessionCoordinator
{
    void EnterOverlayEditSession(PluginSettingsWindow window, OverlayWindow overlayWindow);

    void ExitOverlayEditSession(PluginSettingsWindow window);
}
