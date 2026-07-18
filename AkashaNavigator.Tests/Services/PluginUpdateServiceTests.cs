using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginUpdateServiceTests
{
    private const string PluginId = "akasha-genshin-automation";

    [Fact]
    public async Task CheckAllUpdatesAsync_WhenRemotePackageIsNewer_ReturnsRemoteUpdate()
    {
        var library = CreateLibrary("0.4.2");
        library.Setup(service => service.CheckAllUpdates()).Returns([]);
        var packageService = new Mock<IPluginPackageService>();
        packageService.Setup(service => service.GetRemoteCatalog())
            .Returns([CreateRemoteCatalogEntry("0.4.3")]);
        var service = new PluginUpdateService(
            library.Object,
            CreateManifestService().Object,
            packageService.Object);

        var result = await service.CheckAllUpdatesAsync();

        Assert.True(result.IsSuccess);
        var update = Assert.Single(result.Value!);
        Assert.Equal(PluginId, update.PluginId);
        Assert.Equal("0.4.2", update.CurrentVersion);
        Assert.Equal("0.4.3", update.AvailableVersion);
        Assert.Equal(PluginUpdateSource.RemotePackage, update.Source);
    }

    [Fact]
    public async Task CheckAllUpdatesAsync_WhenBuiltInVersionIsNewer_KeepsBuiltInUpdate()
    {
        var library = CreateLibrary("0.4.2");
        library.Setup(service => service.CheckAllUpdates())
            .Returns(
                [
                    UpdateCheckResult.WithUpdate(
                        PluginId,
                        "0.4.2",
                        "0.4.4",
                        @"C:\builtin")
                ]);
        var packageService = new Mock<IPluginPackageService>();
        packageService.Setup(service => service.GetRemoteCatalog())
            .Returns([CreateRemoteCatalogEntry("0.4.3")]);
        var service = new PluginUpdateService(
            library.Object,
            CreateManifestService().Object,
            packageService.Object);

        var result = await service.CheckAllUpdatesAsync();

        var update = Assert.Single(result.Value!);
        Assert.Equal("0.4.4", update.AvailableVersion);
        Assert.Equal(PluginUpdateSource.BuiltIn, update.Source);
    }

    [Fact]
    public async Task UpdatePluginAsync_WhenUpdateIsRemote_UsesPackageService()
    {
        var library = CreateLibrary("0.4.2");
        var packageService = new Mock<IPluginPackageService>();
        packageService.Setup(
                service => service.InstallOrUpdateAsync(
                    PluginId,
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo
                    {
                        Id = PluginId,
                        Name = "Akasha 原神自动化",
                        Version = "0.4.3"
                    }));
        var service = new PluginUpdateService(
            library.Object,
            CreateManifestService().Object,
            packageService.Object);

        var result = await service.UpdatePluginAsync(
            UpdateCheckResult.WithRemoteUpdate(PluginId, "0.4.2", "0.4.3"));

        Assert.True(result.IsSuccess);
        Assert.Equal("0.4.2", result.OldVersion);
        Assert.Equal("0.4.3", result.NewVersion);
        library.Verify(item => item.UpdatePlugin(It.IsAny<string>()), Times.Never);
        packageService.Verify(
            item => item.InstallOrUpdateAsync(
                PluginId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAllUpdatesAsync_WhenManifestRefreshFails_ReturnsFailure()
    {
        var library = CreateLibrary("0.4.2");
        var manifestService = new Mock<IUpdateManifestService>();
        manifestService.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<UpdateManifest>.Failure(
                    Error.Network("MANIFEST_OFFLINE", "更新清单不可用")));
        var service = new PluginUpdateService(
            library.Object,
            manifestService.Object,
            Mock.Of<IPluginPackageService>());

        var result = await service.CheckAllUpdatesAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("MANIFEST_OFFLINE", result.Error?.Code);
        library.Verify(item => item.CheckAllUpdates(), Times.Never);
    }

    private static Mock<IPluginLibrary> CreateLibrary(string installedVersion)
    {
        var installed = new InstalledPluginInfo
        {
            Id = PluginId,
            Name = "Akasha 原神自动化",
            Version = installedVersion
        };
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPlugins()).Returns([installed]);
        library.Setup(service => service.GetInstalledPluginInfo(PluginId)).Returns(installed);
        return library;
    }

    private static Mock<IUpdateManifestService> CreateManifestService()
    {
        var manifest = new UpdateManifest
        {
            Stable = new AppUpdateChannelInfo { Version = "1.3.0" }
        };
        var service = new Mock<IUpdateManifestService>();
        service.SetupGet(item => item.Current).Returns(manifest);
        service.Setup(item => item.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateManifest>.Success(manifest));
        return service;
    }

    private static PluginCatalogEntry CreateRemoteCatalogEntry(string version)
    {
        return new PluginCatalogEntry
        {
            Id = PluginId,
            Name = "Akasha 原神自动化",
            Version = version,
            IsRemote = true,
            Package = new PluginPackageInfo()
        };
    }
}
