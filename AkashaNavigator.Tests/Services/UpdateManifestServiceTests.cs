using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class UpdateManifestServiceTests : IDisposable
{
    private const string ManifestUrl = "https://updates.example.test/notice.json";

    private readonly string _temporaryDirectory;
    private readonly string _cacheFilePath;
    private readonly string _stateFilePath;
    private readonly ILogService _logService;

    public UpdateManifestServiceTests()
    {
        _temporaryDirectory =
            Path.Combine(Path.GetTempPath(), $"akasha_update_manifest_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _cacheFilePath = Path.Combine(_temporaryDirectory, "notice-cache.json");
        _stateFilePath = Path.Combine(_temporaryDirectory, "notice-state.json");
        _logService = new Mock<ILogService>().Object;
    }

    [Fact]
    public void Deserialize_LegacyNotice_PreservesCompatibility()
    {
        const string json =
            """
            {
              "stable": {
                "version": "1.2.1",
                "source": "qiniu",
                "notes": "stable"
              },
              "alpha": {
                "version": "1.3.0-alpha.4",
                "source": "qiniu",
                "notes": "alpha"
              },
              "min_required_version": "1.2.0"
            }
            """;

        var manifest = JsonHelper.Deserialize<UpdateManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("1.2.1", manifest.Stable?.Version);
        Assert.Equal("1.3.0-alpha.4", manifest.Alpha?.Version);
        Assert.Equal("1.2.0", manifest.MinRequiredVersion);
    }

    [Fact]
    public void Deserialize_NoticeWithLegacyPlugins_IgnoresPluginEntries()
    {
        const string json =
            """
            {
              "schemaVersion": 2,
              "stable": { "version": "1.2.1" },
              "plugins": {
                "akasha-genshin-automation": {
                  "name": "Akasha 原神自动化",
                  "version": "0.3.2",
                  "minHostVersion": "1.3.0-alpha.4",
                  "package": {
                    "fileName": "automation.zip",
                    "size": 68712360,
                    "sha256": "package-hash",
                    "sources": [
                      { "id": "github", "url": "https://github.example/package.zip" },
                      { "id": "cnb", "url": "https://cnb.example/package.zip" }
                    ]
                  },
                  "resources": {
                    "pickBlacklist": {
                      "version": "bettergi-0.62.0",
                      "upstreamRelease": "0.62.0",
                      "minPluginVersion": "0.3.2",
                      "fileName": "default_pick_black_lists.json",
                      "size": 1234,
                      "sha256": "resource-hash",
                      "entryCount": 4914,
                      "url": "https://updates.example/blacklist.json"
                    }
                  }
                }
              }
            }
            """;

        var manifest = JsonHelper.Deserialize<UpdateManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("1.2.1", manifest.Stable?.Version);
    }

    [Fact]
    public async Task RefreshAsync_First200Response_SavesManifestAndHttpState()
    {
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(CreateManifestResponse("1.2.1", "\"manifest-v1\"")));
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.1", result.Value?.Stable?.Version);
        Assert.True(File.Exists(_cacheFilePath));
        Assert.True(File.Exists(_stateFilePath));

        var stateJson = await File.ReadAllTextAsync(_stateFilePath);
        Assert.Contains("\"etag\"", stateJson);
        var state = JsonHelper.Deserialize<UpdateManifestState>(stateJson);
        Assert.Equal("\"manifest-v1\"", state?.ETag);
        Assert.Equal("Thu, 11 Jun 2026 18:02:28 GMT", state?.LastModified);
    }

    [Fact]
    public async Task RefreshAsync_WithSavedEtag_SendsConditionAndReusesCacheOn304()
    {
        await SaveCacheAndStateAsync("1.2.1", "\"manifest-v1\"");
        string? observedEtag = null;
        DateTimeOffset? observedModifiedSince = null;
        using var httpClient = CreateHttpClient(
            (request, _) =>
            {
                observedEtag = request.Headers.IfNoneMatch.Single().ToString();
                observedModifiedSince = request.Headers.IfModifiedSince;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            });
        var service = CreateService(httpClient);

        Assert.Equal("1.2.1", service.Current?.Stable?.Version);
        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.1", result.Value?.Stable?.Version);
        Assert.Equal("\"manifest-v1\"", observedEtag);
        Assert.Equal(DateTimeOffset.Parse("Thu, 11 Jun 2026 18:02:28 GMT"), observedModifiedSince);
    }

    [Fact]
    public async Task RefreshAsync_WhenManifestChanges_ReplacesCacheAndState()
    {
        await SaveCacheAndStateAsync("1.2.1", "\"manifest-v1\"");
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(CreateManifestResponse("1.2.2", "\"manifest-v2\"")));
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.2", result.Value?.Stable?.Version);
        var cached = JsonHelper.LoadFromFile<UpdateManifest>(_cacheFilePath);
        var state = JsonHelper.LoadFromFile<UpdateManifestState>(_stateFilePath);
        Assert.Equal("1.2.2", cached.Value?.Stable?.Version);
        Assert.Equal("\"manifest-v2\"", state.Value?.ETag);
    }

    [Fact]
    public async Task RefreshAsync_WhenNetworkFails_ReturnsCachedManifest()
    {
        await SaveCacheAndStateAsync("1.2.1", "\"manifest-v1\"");
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.1", result.Value?.Stable?.Version);
    }

    [Fact]
    public async Task RefreshAsync_WhenNetworkFailsWithoutCache_ReturnsFailure()
    {
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("UPDATE_MANIFEST_REQUEST_FAILED", result.Error?.Code);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task RefreshAsync_When304HasNoCache_ReturnsFailure()
    {
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("UPDATE_MANIFEST_NOT_MODIFIED_WITHOUT_CACHE", result.Error?.Code);
    }

    [Fact]
    public async Task RefreshAsync_WhenRemoteJsonIsInvalid_DoesNotReplaceCache()
    {
        await SaveCacheAndStateAsync("1.2.1", "\"manifest-v1\"");
        var originalCache = await File.ReadAllTextAsync(_cacheFilePath);
        using var httpClient = CreateHttpClient(
            (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent("{ invalid", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            });
        var service = CreateService(httpClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1.2.1", result.Value?.Stable?.Version);
        Assert.Equal(originalCache, await File.ReadAllTextAsync(_cacheFilePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch
        {
            // 测试临时目录清理是 best effort。
        }
    }

    private UpdateManifestService CreateService(HttpClient httpClient)
    {
        return new UpdateManifestService(
            httpClient,
            new UpdateOptions { ManifestUrl = ManifestUrl },
            _logService,
            _cacheFilePath,
            _stateFilePath);
    }

    private async Task SaveCacheAndStateAsync(string version, string etag)
    {
        var manifest = new UpdateManifest {
            Stable = new AppUpdateChannelInfo { Version = version }
        };
        var state = new UpdateManifestState {
            ETag = etag,
            LastModified = "Thu, 11 Jun 2026 18:02:28 GMT"
        };

        Assert.True(JsonHelper.SaveToFile(_cacheFilePath, manifest).IsSuccess);
        Assert.True(JsonHelper.SaveToFile(_stateFilePath, state).IsSuccess);
        await Task.CompletedTask;
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        return new HttpClient(new DelegateHttpMessageHandler(handler)) {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static HttpResponseMessage CreateManifestResponse(string version, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                $$"""
                  {
                    "schemaVersion": 2,
                    "stable": { "version": "{{version}}" }
                  }
                  """,
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.ETag = new EntityTagHeaderValue(etag);
        response.Content.Headers.LastModified = DateTimeOffset.Parse("Thu, 11 Jun 2026 18:02:28 GMT");
        return response;
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegateHttpMessageHandler(
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
}
