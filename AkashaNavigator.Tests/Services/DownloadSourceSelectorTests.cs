using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class DownloadSourceSelectorTests
{
    private const int ProbeByteCount = 8 * 1024 * 1024;
    private static readonly byte[] ProbePayload = new byte[ProbeByteCount];

    [Theory]
    [InlineData(PluginDownloadSourcePreference.GitHub, "github")]
    [InlineData(PluginDownloadSourcePreference.Cnb, "cnb")]
    public async Task GetOrderedSourcesAsync_ManualPreference_DoesNotProbe(
        PluginDownloadSourcePreference preference,
        string expectedFirst)
    {
        var requestCount = 0;
        using var httpClient = CreateClient(
            (_, _) =>
            {
                requestCount++;
                return Task.FromResult(CreateResponse());
            });
        var selector = CreateSelector(httpClient, preference);

        var result = await selector.GetOrderedSourcesAsync(CreatePackage());

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedFirst, result.Value?.First().Id);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task GetOrderedSourcesAsync_Auto_SelectsFasterSourceAndCachesResult()
    {
        var requestCount = 0;
        using var httpClient = CreateClient(
            async (request, cancellationToken) =>
            {
                Interlocked.Increment(ref requestCount);
                var delay = request.RequestUri!.Host.StartsWith("github", StringComparison.Ordinal)
                    ? 10
                    : 1500;
                await Task.Delay(delay, cancellationToken);
                return CreateResponse();
            });
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);
        var package = CreatePackage();

        var first = await selector.GetOrderedSourcesAsync(package);
        var second = await selector.GetOrderedSourcesAsync(package);

        Assert.True(first.IsSuccess);
        Assert.Equal("github", first.Value?.First().Id);
        Assert.Equal("github", second.Value?.First().Id);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetOrderedSourcesAsync_Auto_SelectsCnbWhenCnbIsFaster()
    {
        using var httpClient = CreateClient(
            async (request, cancellationToken) =>
            {
                var delay = request.RequestUri!.Host.StartsWith("cnb", StringComparison.Ordinal)
                    ? 10
                    : 1500;
                await Task.Delay(delay, cancellationToken);
                return CreateResponse();
            });
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);

        var result = await selector.GetOrderedSourcesAsync(CreatePackage());

        Assert.True(result.IsSuccess);
        Assert.Equal("cnb", result.Value?.First().Id);
    }

    [Fact]
    public async Task MeasureSourcesAsync_ReadsUpToEightMibPerSource()
    {
        using var httpClient = CreateClient((_, _) => Task.FromResult(CreateResponse()));
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);

        var result = await selector.MeasureSourcesAsync(CreatePackage(), forceRefresh: true);

        Assert.True(result.IsSuccess);
        Assert.All(
            result.Value!,
            measurement => Assert.Equal(ProbeByteCount, measurement.BytesRead));
    }

    [Fact]
    public async Task GetOrderedSourcesAsync_UsesSharedMeasurementAcrossPackages()
    {
        var requestCount = 0;
        using var httpClient = CreateClient(
            (request, _) =>
            {
                Interlocked.Increment(ref requestCount);
                return Task.FromResult(CreateResponse());
            });
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);
        var measuredPackage = CreatePackage();
        var anotherPackage = CreatePackage();
        anotherPackage.FileName = "another-plugin.zip";
        anotherPackage.Sources[0].Url = "https://github.example.test/another-plugin.zip";
        anotherPackage.Sources[1].Url = "https://cnb.example.test/another-plugin.zip";

        await selector.MeasureSourcesAsync(measuredPackage, forceRefresh: true);
        var result = await selector.GetOrderedSourcesAsync(anotherPackage);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task ClearCache_NextSelectionProbesAgain()
    {
        var requestCount = 0;
        using var httpClient = CreateClient(
            (request, _) =>
            {
                Interlocked.Increment(ref requestCount);
                return Task.FromResult(CreateResponse());
            });
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);
        var package = CreatePackage();

        await selector.GetOrderedSourcesAsync(package);
        selector.ClearCache();
        await selector.GetOrderedSourcesAsync(package);

        Assert.Equal(4, requestCount);
    }

    [Fact]
    public async Task GetOrderedSourcesAsync_WhenOneProbeFails_KeepsItAsFallback()
    {
        using var httpClient = CreateClient(
            (request, _) =>
            {
                return request.RequestUri!.Host.StartsWith("github", StringComparison.Ordinal)
                    ? Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))
                    : Task.FromResult(CreateResponse());
            });
        var selector = CreateSelector(httpClient, PluginDownloadSourcePreference.Auto);

        var result = await selector.GetOrderedSourcesAsync(CreatePackage());

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "cnb", "github" }, result.Value?.Select(source => source.Id));
    }

    private static DownloadSourceSelector CreateSelector(
        HttpClient httpClient,
        PluginDownloadSourcePreference preference)
    {
        var configService = new Mock<IConfigService>();
        configService.SetupGet(service => service.Config)
            .Returns(new AppConfig { PluginDownloadSourcePreference = preference });
        return new DownloadSourceSelector(httpClient, configService.Object);
    }

    private static PluginPackageInfo CreatePackage()
    {
        return new PluginPackageInfo {
            FileName = "plugin.zip",
            Size = 512 * 1024,
            Sha256 = new string('a', 64),
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

    private static HttpResponseMessage CreateResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.PartialContent) {
            Content = new ByteArrayContent(ProbePayload)
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
            Assert.NotNull(request.Headers.Range);
            Assert.Equal(0, request.Headers.Range!.Ranges.Single().From);
            Assert.Equal(ProbeByteCount - 1, request.Headers.Range.Ranges.Single().To);
            return _handler(request, cancellationToken);
        }
    }
}
