using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class InstalledPluginsPageViewModelTests
{
    [Fact]
    public async Task OnLoadedAsync_UsesSubscriptionCatalogUpdates()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    new[] {
                        new PluginSubscriptionUpdate {
                            PluginId = "sample-plugin",
                            InstalledVersion = "1.0.0",
                            AvailableVersion = "2.0.0"
                        }
                    }));
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out _);

        await viewModel.OnLoadedAsync();

        var plugin = Assert.Single(viewModel.Plugins);
        Assert.True(plugin.HasUpdate);
        Assert.Equal("2.0.0", plugin.AvailableVersion);
        subscriptions.Verify(
            service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePluginCommand_UsesRepositoryInstaller()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    Array.Empty<PluginSubscriptionUpdate>()));
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out var installer);
        installer
            .Setup(service => service.InstallOrUpdateRepositoryPluginAsync(
                "sample-plugin",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "2.0.0"
                    }));

        await viewModel.UpdatePluginCommand.ExecuteAsync("sample-plugin");

        installer.Verify(
            service => service.InstallOrUpdateRepositoryPluginAsync(
                "sample-plugin",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static InstalledPluginsPageViewModel CreateViewModel(
        IPluginSubscriptionService subscriptions,
        out Mock<IPluginInstaller> installer)
    {
        var library = new Mock<IPluginLibrary>();
        library
            .Setup(service => service.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "1.0.0"
                    }
                });
        library
            .Setup(service => service.GetInstalledPluginInfo("sample-plugin"))
            .Returns(
                new InstalledPluginInfo {
                    Id = "sample-plugin",
                    Name = "Sample",
                    Version = "1.0.0"
                });
        var associations = new Mock<IPluginAssociationManager>();
        associations
            .Setup(service => service.GetPluginReferenceCount(
                It.IsAny<string>()))
            .Returns(0);
        associations
            .Setup(service => service.GetProfilesUsingPlugin(
                It.IsAny<string>()))
            .Returns(new List<string>());
        installer = new Mock<IPluginInstaller>();
        return new InstalledPluginsPageViewModel(
            library.Object,
            associations.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEventBus>(),
            subscriptions,
            installer.Object);
    }
}
