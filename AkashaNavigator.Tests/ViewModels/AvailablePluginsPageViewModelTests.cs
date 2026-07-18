using System.IO;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class AvailablePluginsPageViewModelTests
{
    private const string CatalogCommit =
        "89abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void RefreshPluginList_UsesRepositoryIndexAsCatalogAuthority()
    {
        var snapshot = CreateSnapshot(usedCache: true);
        var repositoryService = CreateRepositoryService(snapshot);
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "alpha-plugin",
                        Name = "Old Alpha",
                        Version = "1.0.0"
                    }
                });
        var packageService = new Mock<IPluginPackageService>();
        var viewModel = CreateViewModel(
            repositoryService.Object,
            pluginLibrary.Object,
            packageService.Object);

        viewModel.RefreshPluginList();

        Assert.Equal(2, viewModel.Plugins.Count);
        var alpha = Assert.Single(
            viewModel.Plugins.Where(plugin => plugin.Id == "alpha-plugin"));
        Assert.Equal("2.0.0", alpha.Version);
        Assert.True(alpha.IsInstalled);
        Assert.True(alpha.HasUpdate);
        Assert.Equal(
            Path.Combine("C:\\catalog-cache", "plugins/alpha-plugin"),
            alpha.SourceDirectory);
        packageService.Verify(
            service => service.GetRemoteCatalog(),
            Times.Never);
    }

    [Fact]
    public async Task OnLoadedAsync_ReportsCachedRepositoryAndDoesNotRefreshLegacyManifest()
    {
        var snapshot = CreateSnapshot(usedCache: true);
        var repositoryService = CreateRepositoryService(snapshot);
        repositoryService
            .Setup(service => service.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(snapshot));
        var viewModel = CreateViewModel(repositoryService.Object);

        await viewModel.OnLoadedAsync();

        Assert.Contains("本地缓存", viewModel.RepositoryStatusText);
        Assert.Contains("2 个插件", viewModel.RepositoryStatusText);
        repositoryService.Verify(
            service => service.InitializeAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRepositoryCommand_SavesChannelThenRefreshesCatalog()
    {
        var cached = CreateSnapshot(usedCache: true);
        var fresh = CreateSnapshot(usedCache: false);
        var current = cached;
        PluginRepositorySettings? savedSettings = null;
        var repositoryService = CreateRepositoryService(cached);
        repositoryService
            .SetupGet(service => service.Current)
            .Returns(() => current);
        repositoryService
            .Setup(service => service.SaveSettings(
                It.IsAny<PluginRepositorySettings>()))
            .Callback<PluginRepositorySettings>(settings => savedSettings = settings)
            .Returns(Result.Success());
        repositoryService
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .Callback(() => current = fresh)
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(fresh));
        var viewModel = CreateViewModel(repositoryService.Object);
        viewModel.SelectedRepositoryChannel = PluginRepositoryChannel.Cnb;
        viewModel.AutoUpdateRepository = false;

        await viewModel.UpdateRepositoryCommand.ExecuteAsync(null);

        Assert.Equal(PluginRepositoryChannel.Cnb, savedSettings!.SelectedChannel);
        Assert.False(savedSettings.AutoUpdateRepository);
        Assert.Contains("最新仓库", viewModel.RepositoryStatusText);
        repositoryService.Verify(
            service => service.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AvailablePluginsPageViewModel CreateViewModel(
        IPluginRepositoryService repositoryService,
        IPluginLibrary? pluginLibrary = null,
        IPluginPackageService? packageService = null)
    {
        var library = pluginLibrary ?? CreatePluginLibrary().Object;
        var configService = new Mock<IConfigService>();
        configService.SetupGet(service => service.Config).Returns(new AppConfig());
        return new AvailablePluginsPageViewModel(
            library,
            Mock.Of<INotificationService>(),
            Mock.Of<IEventBus>(),
            repositoryService,
            packageService ?? Mock.Of<IPluginPackageService>(),
            configService.Object);
    }

    private static Mock<IPluginLibrary> CreatePluginLibrary()
    {
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(new List<InstalledPluginInfo>());
        return pluginLibrary;
    }

    private static Mock<IPluginRepositoryService> CreateRepositoryService(
        PluginRepositorySnapshot snapshot)
    {
        var repositoryService = new Mock<IPluginRepositoryService>();
        repositoryService
            .SetupGet(service => service.RepositoryDirectory)
            .Returns("C:\\catalog-cache");
        repositoryService
            .SetupGet(service => service.Current)
            .Returns(snapshot);
        repositoryService
            .SetupGet(service => service.Settings)
            .Returns(new PluginRepositorySettings());
        repositoryService
            .Setup(service => service.SaveSettings(
                It.IsAny<PluginRepositorySettings>()))
            .Returns(Result.Success());
        return repositoryService;
    }

    private static PluginRepositorySnapshot CreateSnapshot(bool usedCache)
    {
        return new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = "0123456789abcdef0123456789abcdef01234567",
                Plugins = new List<PluginRepositoryEntry> {
                    new() {
                        Id = "alpha-plugin",
                        Path = "plugins/alpha-plugin",
                        Name = "Alpha",
                        Version = "2.0.0",
                        Description = "Alpha description",
                        DistributionType = AppConstants.PluginDistributionRepository,
                        MinHostVersion = "1.4.0"
                    },
                    new() {
                        Id = "beta-plugin",
                        Path = "plugins/beta-plugin",
                        Name = "Beta",
                        Version = "1.0.0",
                        Description = "Beta description",
                        DistributionType = AppConstants.PluginDistributionRelease,
                        HasBackend = true,
                        MinHostVersion = "1.4.0"
                    }
                }
            },
            CatalogCommit,
            usedCache);
    }
}
