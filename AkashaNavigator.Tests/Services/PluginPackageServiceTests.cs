using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginPackageServiceTests
{
    private const string PluginId = "akasha-genshin-automation";

    [Fact]
    public async Task DownloadPackageAsync_WhenFirstSourceHashFails_UsesSecondSource()
    {
        var validBytes = Enumerable.Range(0, 2048)
            .Select(index => (byte)(index % 251))
            .ToArray();
        var invalidBytes = validBytes.ToArray();
        invalidBytes[0] ^= 0xff;
        var package = CreatePackage(validBytes);
        var selector = CreateSelector(package.Sources);
        var attemptedHosts = new List<string>();
        using var httpClient = CreateClient(
            (request, _) =>
            {
                attemptedHosts.Add(request.RequestUri!.Host);
                var bytes = request.RequestUri.Host.StartsWith(
                    "github",
                    StringComparison.Ordinal)
                    ? invalidBytes
                    : validBytes;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new ByteArrayContent(bytes)
                    });
            });
        var progress = new SynchronousProgress();
        var service = new PluginPackageService(httpClient, selector.Object);

        var result = await service.DownloadPackageAsync(
            PluginId,
            package,
            progress);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            new[] { "github.example.test", "cnb.example.test" },
            attemptedHosts);
        Assert.Contains(
            progress.Values,
            value => value.SourceId == "cnb" && value.Percentage == 100);
        var downloaded = result.Value!;
        Assert.True(File.Exists(downloaded.FilePath));
        downloaded.Dispose();
        Assert.False(File.Exists(downloaded.FilePath));
    }

    [Fact]
    public async Task DownloadPackageAsync_WhenAllSourcesFail_ReturnsAttempts()
    {
        var package = CreatePackage(new byte[1024]);
        var selector = CreateSelector(package.Sources);
        using var httpClient = CreateClient(
            (_, _) => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("offline")));
        var service = new PluginPackageService(httpClient, selector.Object);

        var result = await service.DownloadPackageAsync(PluginId, package);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.RemoteDownloadFailed, result.Error?.Code);
        var attempted = Assert.IsType<string[]>(
            result.Error?.Metadata["AttemptedSources"]);
        Assert.Equal(new[] { "github", "cnb" }, attempted);
    }

    [Fact]
    public async Task DownloadPackageAsync_WhenCanceled_ReturnsCanceledResult()
    {
        var package = CreatePackage(new byte[1024]);
        var selector = CreateSelector(package.Sources);
        using var httpClient = CreateClient(
            async (_, cancellationToken) =>
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var service = new PluginPackageService(httpClient, selector.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await service.DownloadPackageAsync(
            PluginId,
            package,
            cancellationToken: cancellation.Token);

        Assert.True(result.IsFailure);
        Assert.Equal(
            PluginErrorCodes.RemoteDownloadCanceled,
            result.Error?.Code);
    }

    [Fact]
    public async Task DownloadPackageAsync_WhenMetadataIsInvalid_DoesNotSelectSource()
    {
        var selector = new Mock<IDownloadSourceSelector>();
        var service = new PluginPackageService(
            new HttpClient(),
            selector.Object);
        var package = CreatePackage(new byte[32]);
        package.FileName = "../plugin.zip";

        var result = await service.DownloadPackageAsync(PluginId, package);

        Assert.True(result.IsFailure);
        Assert.Equal(
            PluginErrorCodes.RemotePackageNotFound,
            result.Error?.Code);
        selector.Verify(
            item => item.GetOrderedSourcesAsync(
                It.IsAny<PluginPackageInfo>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IDownloadSourceSelector> CreateSelector(
        IReadOnlyList<DownloadSourceInfo> sources)
    {
        var selector = new Mock<IDownloadSourceSelector>();
        selector.Setup(
                service => service.GetOrderedSourcesAsync(
                    It.IsAny<PluginPackageInfo>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<DownloadSourceInfo>>.Success(
                    sources));
        return selector;
    }

    private static PluginPackageInfo CreatePackage(byte[] bytes)
    {
        return new PluginPackageInfo {
            FileName = "plugin.zip",
            Size = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant(),
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
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            handler)
    {
        return new HttpClient(new DelegateHandler(handler)) {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public DelegateHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
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

    private sealed class SynchronousProgress :
        IProgress<PluginDownloadProgress>
    {
        public List<PluginDownloadProgress> Values { get; } = new();

        public void Report(PluginDownloadProgress value)
        {
            Values.Add(value);
        }
    }
}
