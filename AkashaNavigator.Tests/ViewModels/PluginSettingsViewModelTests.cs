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
}
