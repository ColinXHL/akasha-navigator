using System;
using System.IO;
using System.Threading.Tasks;
using AkashaNavigator.Tests.TestDoubles;
using AkashaNavigator.ViewModels.Windows;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public class PluginSettingsViewModelTests
{
    [Fact]
    public async Task SaveAsync_ReloadsPlugin_WhenConfigWasModified()
    {
        var pluginHost = new FakePluginHost();
        var configDirectory = Path.Combine(Path.GetTempPath(), "AkashaNavigator", Guid.NewGuid().ToString("N"));

        var viewModel = new PluginSettingsViewModel(
            new FakeProfileManager(),
            new FakeLogService(),
            pluginHost,
            new FakeNotificationService(),
            "plugin.alpha",
            "Alpha",
            @"C:\plugin",
            configDirectory,
            "profile-1");

        viewModel.UpdateValue("enabled", true);
        await viewModel.SaveAsync();

        Assert.Equal("plugin.alpha", pluginHost.ReloadedPluginId);
    }

    [Fact]
    public void Constructor_LoadsSettingsFromManifestDeclaredPath()
    {
        var pluginDirectory = Path.Combine(
            Path.GetTempPath(), "AkashaNavigator", Guid.NewGuid().ToString("N"));
        var settingsDirectory = Path.Combine(pluginDirectory, "frontend");
        var configDirectory = Path.Combine(pluginDirectory, "config");
        Directory.CreateDirectory(settingsDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, AppConstants.PluginManifestFileName),
            """
            {
              "id": "plugin.alpha",
              "name": "Alpha",
              "version": "1.0.0",
              "main": "frontend/main.js"
            }
            """);
        File.WriteAllText(
            Path.Combine(pluginDirectory, AppConstants.PluginRepositoryManifestFileName),
            """
            {
              "settings": "frontend/settings_ui.json"
            }
            """);
        File.WriteAllText(
            Path.Combine(settingsDirectory, AppConstants.PluginSettingsUiFileName),
            """
            {
              "sections": [
                {
                  "title": "功能开关",
                  "items": [
                    {
                      "type": "checkbox",
                      "key": "enabled",
                      "label": "启用"
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            var viewModel = new PluginSettingsViewModel(
                new FakeProfileManager(),
                new FakeLogService(),
                new FakePluginHost(),
                new FakeNotificationService(),
                "plugin.alpha",
                "Alpha",
                pluginDirectory,
                configDirectory,
                "profile-1");

            var section = Assert.Single(viewModel.SettingsDefinition!.Sections!);
            Assert.Equal("功能开关", section.Title);
            Assert.Equal("enabled", Assert.Single(section.Items!).Key);
        }
        finally
        {
            Directory.Delete(pluginDirectory, recursive: true);
        }
    }
}
