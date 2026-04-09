using System;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.ViewModels.Windows;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services;

public class PluginSettingsWindowService : IPluginSettingsWindowService
{
    private readonly Func<string, string, string, string, string?, PluginSettingsViewModel> _viewModelFactory;
    private readonly Func<PluginSettingsViewModel, PluginSettingsWindow> _windowFactory;

    public PluginSettingsWindowService(
        Func<string, string, string, string, string?, PluginSettingsViewModel> viewModelFactory,
        Func<PluginSettingsViewModel, PluginSettingsWindow> windowFactory)
    {
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
    }

    public void Show(string pluginId, string pluginName, string pluginDirectory, string configDirectory,
                     Window? owner = null, string? profileId = null)
    {
        var viewModel = _viewModelFactory(pluginId, pluginName, pluginDirectory, configDirectory, profileId);
        var window = _windowFactory(viewModel);

        if (owner != null)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.Show();
    }
}
