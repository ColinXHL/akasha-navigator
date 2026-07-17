using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Update;
using AkashaNavigator.ViewModels.Pages.Settings;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class AdvancedSettingsPageViewModelTests
{
    [Fact]
    public void LoadAndSaveSettings_PreservesDownloadSourcePreference()
    {
        var selector = new Mock<IDownloadSourceSelector>();
        var viewModel = CreateViewModel(selector.Object);
        var source = new AppConfig {
            PluginDownloadSourcePreference = PluginDownloadSourcePreference.Cnb
        };

        viewModel.LoadSettings(source);
        var saved = new AppConfig();
        viewModel.SaveSettings(saved);

        Assert.Equal(PluginDownloadSourcePreference.Cnb, viewModel.PluginDownloadSourcePreference);
        Assert.Equal(PluginDownloadSourcePreference.Cnb, saved.PluginDownloadSourcePreference);
    }

    [Fact]
    public void RemeasurePluginSourcesCommand_ClearsSelectorCache()
    {
        var selector = new Mock<IDownloadSourceSelector>();
        var viewModel = CreateViewModel(selector.Object);

        viewModel.RemeasurePluginSourcesCommand.Execute(null);

        selector.Verify(service => service.ClearCache(), Times.Once);
    }

    private static AdvancedSettingsPageViewModel CreateViewModel(
        IDownloadSourceSelector selector)
    {
        return new AdvancedSettingsPageViewModel(
            Mock.Of<IAppUpdateService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<ILogService>(),
            selector);
    }
}
