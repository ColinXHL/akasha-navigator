using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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

public sealed class PluginPackageServiceTests
{
    private const string PluginId = "akasha-genshin-automation";

    [Fact]
    public async Task InstallOrUpdateAsync_WhenFirstSourceHashFails_UsesSecondSource()
    {
        var validBytes = Enumerable.Range(0, 2048).Select(index => (byte)(index % 251)).ToArray();
        var invalidBytes = validBytes.ToArray();
        invalidBytes[0] ^= 0xff;
        var package = CreatePackage(validBytes);
        var manifestService = CreateManifestService(package);
        var selector = CreateSelector(package.Sources);
        var attemptedHosts = new List<string>();
        string? installedPath = null;
        using var httpClient = CreateClient(
            (request, _) =>
            {
                attemptedHosts.Add(request.RequestUri!.Host);
                var bytes = request.RequestUri.Host.StartsWith("github", StringComparison.Ordinal)
                    ? invalidBytes
                    : validBytes;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new ByteArrayContent(bytes)
                    });
            });
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPluginInfo(PluginId))
            .Returns((InstalledPluginInfo?)null);
        library.Setup(service => service.InstallPluginPackage(It.IsAny<string>()))
            .Callback<string>(
                path =>
                {
                    installedPath = path;
                    Assert.True(File.Exists(path));
                })
            .Returns(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = PluginId,
                        Name = "Automation",
                        Version = "0.3.2"
                    }));
        var progress = new SynchronousProgress();
        var service = new PluginPackageService(
            httpClient,
            manifestService.Object,
            selector.Object,
            library.Object,
            () => "1.3.0-alpha.4");

        var result = await service.InstallOrUpdateAsync(PluginId, progress);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "github.example.test", "cnb.example.test" }, attemptedHosts);
        Assert.Contains(progress.Values, value => value.SourceId == "cnb" && value.Percentage == 100);
        Assert.NotNull(installedPath);
        Assert.False(File.Exists(installedPath));
    }

    [Fact]
    public async Task InstallOrUpdateAsync_WhenAllSourcesFail_ReturnsAttempts()
    {
        var validBytes = new byte[1024];
        var package = CreatePackage(validBytes);
        var manifestService = CreateManifestService(package);
        var selector = CreateSelector(package.Sources);
        using var httpClient = CreateClient(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPluginInfo(PluginId))
            .Returns((InstalledPluginInfo?)null);
        var service = new PluginPackageService(
            httpClient,
            manifestService.Object,
            selector.Object,
            library.Object,
            () => "1.3.0-alpha.4");

        var result = await service.InstallOrUpdateAsync(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.RemoteDownloadFailed, result.Error?.Code);
        var attempted = Assert.IsType<string[]>(result.Error?.Metadata["AttemptedSources"]);
        Assert.Equal(new[] { "github", "cnb" }, attempted);
        library.Verify(
            service => service.InstallPluginPackage(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallOrUpdateAsync_WhenHostVersionIsTooLow_DoesNotDownload()
    {
        var package = CreatePackage(new byte[1024]);
        var manifestService = CreateManifestService(package);
        var selector = CreateSelector(package.Sources);
        var requestCount = 0;
        using var httpClient = CreateClient(
            (_, _) =>
            {
                requestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPluginInfo(PluginId))
            .Returns((InstalledPluginInfo?)null);
        var service = new PluginPackageService(
            httpClient,
            manifestService.Object,
            selector.Object,
            library.Object,
            () => "1.2.0");

        var result = await service.InstallOrUpdateAsync(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.HostVersionTooLow, result.Error?.Code);
        Assert.Equal(0, requestCount);
        selector.Verify(
            service => service.GetOrderedSourcesAsync(
                It.IsAny<PluginPackageInfo>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallOrUpdateAsync_WhenCanceled_ReturnsCanceledResult()
    {
        var package = CreatePackage(new byte[1024]);
        var manifestService = CreateManifestService(package);
        var selector = CreateSelector(package.Sources);
        using var httpClient = CreateClient(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPluginInfo(PluginId))
            .Returns((InstalledPluginInfo?)null);
        var service = new PluginPackageService(
            httpClient,
            manifestService.Object,
            selector.Object,
            library.Object,
            () => "1.3.0-alpha.4");
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await service.InstallOrUpdateAsync(
            PluginId,
            cancellationToken: cancellation.Token);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.RemoteDownloadCanceled, result.Error?.Code);
        library.Verify(
            item => item.InstallPluginPackage(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void GetRemoteCatalog_ReturnsManifestPlugins()
    {
        var package = CreatePackage(new byte[32]);
        var manifestService = CreateManifestService(package);
        var service = new PluginPackageService(
            new HttpClient(),
            manifestService.Object,
            Mock.Of<IDownloadSourceSelector>(),
            Mock.Of<IPluginLibrary>(),
            () => "1.3.0-alpha.4");

        var catalog = service.GetRemoteCatalog();

        var entry = Assert.Single(catalog);
        Assert.Equal(PluginId, entry.Id);
        Assert.True(entry.IsRemote);
        Assert.Null(entry.LocalSourceDirectory);
        Assert.Same(package, entry.Package);
    }

    private static Mock<IUpdateManifestService> CreateManifestService(PluginPackageInfo package)
    {
        var manifest = new UpdateManifest {
            Stable = new AppUpdateChannelInfo { Version = "1.2.1" },
            Plugins = {
                [PluginId] = new RemotePluginInfo {
                    Name = "Akasha 原神自动化",
                    Version = "0.3.2",
                    MinHostVersion = "1.3.0-alpha.4",
                    Package = package
                }
            }
        };
        var service = new Mock<IUpdateManifestService>();
        service.SetupGet(item => item.Current).Returns(manifest);
        service.Setup(item => item.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateManifest>.Success(manifest));
        return service;
    }

    private static Mock<IDownloadSourceSelector> CreateSelector(
        IReadOnlyList<DownloadSourceInfo> sources)
    {
        var selector = new Mock<IDownloadSourceSelector>();
        selector.Setup(
                service => service.GetOrderedSourcesAsync(
                    It.IsAny<PluginPackageInfo>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<DownloadSourceInfo>>.Success(sources));
        return selector;
    }

    private static PluginPackageInfo CreatePackage(byte[] bytes)
    {
        return new PluginPackageInfo {
            FileName = "plugin.zip",
            Size = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Sources = {
                new DownloadSourceInfo {
                    Id = "github",
                    Url = "https://github.example.test/plugin.zip"
                },
                new DownloadSourceInfo {
                    Id = "cnb",
                    Url = "https://cnb.example.test/plugin.zip"
                }
            }
        };
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        return new HttpClient(new DelegateHandler(handler)) {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegateHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class SynchronousProgress : IProgress<PluginDownloadProgress>
    {
        public List<PluginDownloadProgress> Values { get; } = new();

        public void Report(PluginDownloadProgress value)
        {
            Values.Add(value);
        }
    }
}
