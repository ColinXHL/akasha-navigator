using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginResourceUpdateServiceTests
{
    [Fact]
    public async Task UpdatePickBlacklistAsync_WhenPluginIsNotInstalled_DoesNotDownload()
    {
        var requestCount = 0;
        using var environment = CreateEnvironment(
            Encoding.UTF8.GetBytes("[\"远程条目\"]"),
            installedVersion: null,
            onRequest: () => requestCount++);

        var result = await environment.Service.UpdatePickBlacklistAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginResourceUpdateStatus.PluginNotInstalled, result.Value?.Status);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task UpdatePickBlacklistAsync_WhenPluginVersionIsTooLow_DoesNotDownload()
    {
        var requestCount = 0;
        using var environment = CreateEnvironment(
            Encoding.UTF8.GetBytes("[\"远程条目\"]"),
            installedVersion: "0.3.1",
            onRequest: () => requestCount++);

        var result = await environment.Service.UpdatePickBlacklistAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginResourceUpdateStatus.PluginVersionTooLow, result.Value?.Status);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task UpdatePickBlacklistAsync_ValidResource_ReplacesCurrentAndWritesState()
    {
        var bytes = Encoding.UTF8.GetBytes("[\"远程条目\",\"第二条\"]");
        using var environment = CreateEnvironment(bytes, workerRunning: true, entryCount: 2);
        Directory.CreateDirectory(Path.GetDirectoryName(environment.CurrentPath)!);
        await File.WriteAllTextAsync(environment.CurrentPath, "[\"旧条目\"]");

        var result = await environment.Service.UpdatePickBlacklistAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PluginResourceUpdateStatus.Updated, result.Value?.Status);
        Assert.True(result.Value?.TakesEffectOnNextWorkerStart);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(environment.CurrentPath));
        var state = Assert.IsType<PluginResourceState>(
            AkashaNavigator.Helpers.JsonHelper
                .LoadFromFile<PluginResourceState>(environment.StatePath)
                .Value);
        Assert.Equal("bettergi-test", state.Version);
        Assert.Equal(2, state.EntryCount);
        Assert.Equal(environment.Sha256, state.Sha256);
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(environment.CurrentPath)!,
            "*.download"));
    }

    [Fact]
    public async Task UpdatePickBlacklistAsync_InvalidJson_PreservesPreviousResource()
    {
        var bytes = Encoding.UTF8.GetBytes("{ invalid-json");
        using var environment = CreateEnvironment(bytes, entryCount: 1);
        Directory.CreateDirectory(Path.GetDirectoryName(environment.CurrentPath)!);
        const string previous = "[\"旧条目\"]";
        await File.WriteAllTextAsync(environment.CurrentPath, previous);

        var result = await environment.Service.UpdatePickBlacklistAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(PluginResourceErrorCodes.ContentInvalid, result.Error?.Code);
        Assert.Equal(previous, await File.ReadAllTextAsync(environment.CurrentPath));
        Assert.False(File.Exists(environment.StatePath));
    }

    [Fact]
    public async Task UpdatePickBlacklistAsync_WhenLocalHashMatches_DoesNotDownload()
    {
        var bytes = Encoding.UTF8.GetBytes("[\"远程条目\"]");
        var requestCount = 0;
        using var environment = CreateEnvironment(bytes, onRequest: () => requestCount++);
        Directory.CreateDirectory(Path.GetDirectoryName(environment.CurrentPath)!);
        await File.WriteAllBytesAsync(environment.CurrentPath, bytes);

        var result = await environment.Service.UpdatePickBlacklistAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginResourceUpdateStatus.UpToDate, result.Value?.Status);
        Assert.Equal(0, requestCount);
        Assert.True(File.Exists(environment.StatePath));
    }

    private static TestEnvironment CreateEnvironment(
        byte[] content,
        string? installedVersion = "0.3.2",
        bool workerRunning = false,
        int? entryCount = null,
        Action? onRequest = null)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginResourceTests.{Guid.NewGuid():N}");
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var manifest = new UpdateManifest {
            Stable = new AppUpdateChannelInfo { Version = "1.2.1" },
            Plugins = {
                [AppConstants.AutomationPluginId] = new RemotePluginInfo {
                    Version = "0.3.2",
                    Resources = {
                        [AppConstants.PickBlacklistResourceKey] = new PluginResourceInfo {
                            Version = "bettergi-test",
                            UpstreamRelease = "test",
                            MinPluginVersion = "0.3.2",
                            FileName = "default_pick_black_lists.json",
                            Size = content.Length,
                            Sha256 = sha256,
                            EntryCount = entryCount ?? 1,
                            Url = "https://resources.example.test/pick-blacklist.json"
                        }
                    }
                }
            }
        };
        var manifestService = new Mock<IUpdateManifestService>();
        manifestService.SetupGet(service => service.Current).Returns(manifest);
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPluginInfo(AppConstants.AutomationPluginId))
            .Returns(
                installedVersion == null
                    ? null
                    : new InstalledPluginInfo {
                        Id = AppConstants.AutomationPluginId,
                        Version = installedVersion
                    });
        var companion = new Mock<ICompanionProcessManager>();
        companion.Setup(service => service.GetStatus(AppConstants.AutomationPluginId))
            .Returns(new CompanionStatus(workerRunning, workerRunning ? "running" : "stopped"));
        var httpClient = new HttpClient(
            new DelegateHandler(
                (_, _) =>
                {
                    onRequest?.Invoke();
                    return Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK) {
                            Content = new ByteArrayContent(content)
                        });
                })) {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var service = new PluginResourceUpdateService(
            httpClient,
            manifestService.Object,
            library.Object,
            companion.Object,
            Mock.Of<ILogService>(),
            root);
        return new TestEnvironment(root, sha256, httpClient, service);
    }

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment(
            string root,
            string sha256,
            HttpClient httpClient,
            PluginResourceUpdateService service)
        {
            Root = root;
            Sha256 = sha256;
            HttpClient = httpClient;
            Service = service;
        }

        public string Root { get; }
        public string Sha256 { get; }
        public HttpClient HttpClient { get; }
        public PluginResourceUpdateService Service { get; }
        public string CurrentPath => Path.Combine(
            Root,
            AppConstants.AutomationPluginId,
            "pick-blacklist",
            "current.json");
        public string StatePath => Path.Combine(
            Root,
            AppConstants.AutomationPluginId,
            "pick-blacklist",
            "state.json");

        public void Dispose()
        {
            HttpClient.Dispose();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
