using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Windows;

[Collection(PlayerWindowPlaybackRateSyncCollection.Name)]
public class PlayerWindowPlaybackRateSyncTests
{
    [Fact]
    public async Task IncreasePlaybackRateAsync_ShouldUseLiveVideoRateBeforeCachedRate()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 1.5, currentPlaybackRate: 1.0);

            await harness.Window.IncreasePlaybackRateAsync();

            Assert.Equal(1.75, harness.Window.CurrentPlaybackRate, 3);

            var trackedRate = await harness.GetTrackedPlaybackRateAsync();
            Assert.NotNull(trackedRate);
            Assert.Equal(1.75, trackedRate.Value, 3);

            Assert.Equal(1, await harness.GetPlaybackRateReadsBeforeHostSetAsync());
        });
    }

    [Fact]
    public async Task IncreasePlaybackRateAsync_ShouldFallBackToCachedRateWhenLiveReadFails()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: null, currentPlaybackRate: 1.25);

            await harness.Window.IncreasePlaybackRateAsync();

            Assert.Equal(1.5, harness.Window.CurrentPlaybackRate, 3);

            var trackedRate = await harness.GetTrackedPlaybackRateAsync();
            Assert.NotNull(trackedRate);
            Assert.Equal(1.5, trackedRate.Value, 3);

            Assert.Equal(1, await harness.GetPlaybackRateReadsBeforeHostSetAsync());
        });
    }

    [Fact]
    public async Task IncreasePlaybackRateAsync_ShouldFallBackToCachedRateWhenLiveReadIsOutOfRange()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 5.0, currentPlaybackRate: 1.25);

            await harness.Window.IncreasePlaybackRateAsync();

            Assert.Equal(1.5, harness.Window.CurrentPlaybackRate, 3);

            var trackedRate = await harness.GetTrackedPlaybackRateAsync();
            Assert.NotNull(trackedRate);
            Assert.Equal(1.5, trackedRate.Value, 3);

            harness.LogService.Verify(x => x.Debug(nameof(PlayerWindow), It.IsAny<string>(), It.IsAny<object?[]>()), Times.AtLeastOnce);
            harness.LogService.Verify(x => x.Error(nameof(PlayerWindow), It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
        });
    }

    [Fact]
    public async Task WebMessageReceived_PlaybackRateSyncFromBilibili_ShouldUpdateCurrentPlaybackRate()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 1.0, currentPlaybackRate: 1.0);

            harness.SimulateWebMessage(
                "{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"source\":\"bilibili-rate-menu\",\"url\":\"https://www.bilibili.com/video/BV1test\"}",
                "https://www.bilibili.com/video/BV1test");

            Assert.Equal(1.5, harness.Window.CurrentPlaybackRate, 3);
        });
    }

    [Fact]
    public async Task WebMessageReceived_PlaybackRateSyncFromNonBilibiliSenderWithSpoofedBilibiliPayload_ShouldBeIgnored()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 1.0, currentPlaybackRate: 1.0);

            harness.SimulateWebMessage(
                "{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"source\":\"bilibili-rate-menu\",\"url\":\"https://www.bilibili.com/video/BV1test\"}",
                "https://www.youtube.com/watch?v=test");

            Assert.Equal(1.0, harness.Window.CurrentPlaybackRate, 3);
        });
    }

    [Fact]
    public async Task WebMessageReceived_PlaybackRateSyncFromBilibiliWithoutSource_ShouldBeIgnored()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 1.0, currentPlaybackRate: 1.0);

            harness.SimulateWebMessage(
                "{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"url\":\"https://www.bilibili.com/video/BV1test\"}",
                "https://www.bilibili.com/video/BV1test");

            Assert.Equal(1.0, harness.Window.CurrentPlaybackRate, 3);
        });
    }

    [Fact]
    public async Task WebMessageReceived_PlaybackRateSyncFromBilibiliWithUnknownSource_ShouldBeIgnored()
    {
        await StaTestHost.RunAsync(async () =>
        {
            await using var harness = await PlayerWindowPlaybackRateHarness.CreateAsync(livePlaybackRate: 1.0, currentPlaybackRate: 1.0);

            harness.SimulateWebMessage(
                "{\"type\":\"playback_rate_sync\",\"rate\":1.5,\"source\":\"unknown\",\"url\":\"https://www.bilibili.com/video/BV1test\"}",
                "https://www.bilibili.com/video/BV1test");

            Assert.Equal(1.0, harness.Window.CurrentPlaybackRate, 3);
        });
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlayerWindowPlaybackRateSyncCollection
{
    public const string Name = "PlayerWindowPlaybackRateSync";
}

internal static class StaTestHost
{
    public static Task RunAsync(Func<Task> testBody)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            _ = ExecuteAsync(testBody, completion, Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completion.Task;
    }

    private static async Task ExecuteAsync(Func<Task> testBody, TaskCompletionSource completion, Dispatcher dispatcher)
    {
        try
        {
            await testBody();
            completion.SetResult();
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }
        finally
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }
}

internal sealed class PlayerWindowPlaybackRateHarness : IAsyncDisposable
{
    private readonly Window _hostWindow;
    private readonly WebView2 _webView;
    private readonly Mock<ILogService> _logService;

    private PlayerWindowPlaybackRateHarness(PlayerWindow window, Window hostWindow, WebView2 webView, Mock<ILogService> logService)
    {
        Window = window;
        _hostWindow = hostWindow;
        _webView = webView;
        _logService = logService;
    }

    public PlayerWindow Window { get; }

    public Mock<ILogService> LogService => _logService;

    public static async Task<PlayerWindowPlaybackRateHarness> CreateAsync(double? livePlaybackRate, double currentPlaybackRate)
    {
        var hostWindow = new Window
        {
            Width = 320,
            Height = 180,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Left = -10000,
            Top = -10000
        };

        var webView = new WebView2();
        hostWindow.Content = webView;
        hostWindow.Show();

        await webView.EnsureCoreWebView2Async();
        await NavigateToPlaybackTrackingPageAsync(webView, livePlaybackRate);

        var logService = new Mock<ILogService>(MockBehavior.Loose);
        var scriptQueue = new ScriptExecutionQueue(logService.Object);
        var playerWindow = (PlayerWindow)RuntimeHelpers.GetUninitializedObject(typeof(PlayerWindow));

        SetField(playerWindow, "WebView", webView);
        SetField(playerWindow, "_scriptQueue", scriptQueue);
        SetField(playerWindow, "_logService", logService.Object);
        SetField(playerWindow, "_currentPlaybackRate", currentPlaybackRate);

        return new PlayerWindowPlaybackRateHarness(playerWindow, hostWindow, webView, logService);
    }

    public async Task<double?> GetTrackedPlaybackRateAsync()
    {
        var raw = await _webView.CoreWebView2.ExecuteScriptAsync("window.__akashaPlaybackRateValue");
        return DeserializeDouble(raw);
    }

    public async Task<int> GetPlaybackRateReadsBeforeHostSetAsync()
    {
        var raw = await _webView.CoreWebView2.ExecuteScriptAsync("window.__akashaReadCountBeforeHostSet ?? -1");
        return JsonSerializer.Deserialize<int>(raw);
    }

    public void SimulateWebMessage(string message, string senderSource = "")
    {
        var rawInterfaceType = typeof(CoreWebView2WebMessageReceivedEventArgs).Assembly
            .GetType("Microsoft.Web.WebView2.Core.Raw.ICoreWebView2WebMessageReceivedEventArgs", throwOnError: true)!;

        var createProxy = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(method => method.Name == nameof(DispatchProxy.Create) && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2)
            ?? throw new InvalidOperationException("DispatchProxy.Create was not found.");

        var rawArgs = (MessageReceivedEventArgsProxy)createProxy
            .MakeGenericMethod(rawInterfaceType, typeof(MessageReceivedEventArgsProxy))
            .Invoke(null, null)!;

        rawArgs.Message = message;
        rawArgs.Source = senderSource;

        var args = (CoreWebView2WebMessageReceivedEventArgs)Activator.CreateInstance(
            typeof(CoreWebView2WebMessageReceivedEventArgs),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { rawArgs },
            culture: null)!;

        var handler = typeof(PlayerWindow).GetMethod("CoreWebView2_WebMessageReceived",
                                                     BindingFlags.Instance | BindingFlags.NonPublic)
                      ?? throw new InvalidOperationException("CoreWebView2_WebMessageReceived was not found.");

        handler.Invoke(Window, new object?[] { _webView.CoreWebView2, args });
    }

    private class MessageReceivedEventArgsProxy : DispatchProxy
    {
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "TryGetWebMessageAsString" => Message,
                "get_webMessageAsJson" => Message,
                "get_Source" => Source,
                _ => throw new NotSupportedException($"Unexpected member: {targetMethod?.Name}")
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        _hostWindow.Close();
        await Dispatcher.Yield(DispatcherPriority.Background);
    }

    private static async Task NavigateToPlaybackTrackingPageAsync(WebView2 webView, double? livePlaybackRate)
    {
        var navigationCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            navigationCompleted.TrySetResult();
        }

        webView.CoreWebView2.NavigationCompleted += HandleNavigationCompleted;

        try
        {
            var initialValue = livePlaybackRate.HasValue
                ? livePlaybackRate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "null";

            var html = $$"""
<!DOCTYPE html>
<html>
<body>
    <video id="tracked-video"></video>
    <script>
        const video = document.getElementById('tracked-video');
        window.__akashaPlaybackRateValue = {{initialValue}};
        window.__akashaPlaybackRateReadHistory = [];
        window.__akashaReadCountBeforeHostSet = -1;

        window.addEventListener('akasha:set-playback-rate', () => {
            window.__akashaReadCountBeforeHostSet = window.__akashaPlaybackRateReadHistory.length;
        });

        Object.defineProperty(video, 'playbackRate', {
            configurable: true,
            get() {
                window.__akashaPlaybackRateReadHistory.push(window.__akashaPlaybackRateValue);
                return window.__akashaPlaybackRateValue;
            },
            set(value) {
                window.__akashaPlaybackRateValue = value;
            }
        });
    </script>
</body>
</html>
""";

            webView.NavigateToString(html);
            await navigationCompleted.Task;
        }
        finally
        {
            webView.CoreWebView2.NavigationCompleted -= HandleNavigationCompleted;
        }
    }

    private static double? DeserializeDouble(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonSerializer.Deserialize<double?>(raw);
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }
}
