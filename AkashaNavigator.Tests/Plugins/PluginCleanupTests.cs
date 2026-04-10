using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Xunit;

namespace AkashaNavigator.Tests.Plugins;

public sealed class PluginCleanupTests
{
    [Fact]
    public void Cleanup_ShouldDisposeHttpApi_AndDetachWindowCursorHandlers()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"AkashaNavigator.PluginCleanup.{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var manifest = new PluginManifest
            {
                Id = "plugin.cleanup",
                Name = "Cleanup Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = new List<string> { PluginPermissions.Network, PluginPermissions.Window }
            };

            var context = new PluginContext("plugin.cleanup", tempDirectory, tempDirectory, manifest);
            var cursorDetectionService = new TrackingCursorDetectionService();
            var pluginApi = new PluginApi(context,
                                          new PluginConfig("plugin.cleanup"),
                                          new ProfileInfo("profile-test", "Profile Test", tempDirectory),
                                          new NullPlayerRuntimeBridge(),
                                          cursorDetectionService,
                                          new OverlayManager(),
                                          new PanelManager(),
                                          new SubtitleService(new FakeLogService()),
                                          new ScriptExecutionQueue(new FakeLogService()),
                                          new HotkeyService(),
                                          new OsdManager(),
                                          new FakeLogService());

            var windowApi = Assert.IsType<WindowApi>(pluginApi.Window);
            var httpApi = Assert.IsType<HttpApi>(pluginApi.Http);

            dynamic options = new ExpandoObject();
            options.processWhitelist = new List<string> { "game.exe" };
            options.intervalMs = 200;

            Assert.True(windowApi.StartCursorDetection(options));

            pluginApi.Cleanup();
            pluginApi.Cleanup();

            Assert.True(IsHttpApiDisposed(httpApi));
            Assert.Equal(1, cursorDetectionService.RemovedShownHandlers);
            Assert.Equal(1, cursorDetectionService.RemovedHiddenHandlers);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static bool IsHttpApiDisposed(HttpApi httpApi)
    {
        var disposedField = typeof(HttpApi)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .SingleOrDefault(f => f.FieldType == typeof(bool) &&
                                  f.Name.Contains("disposed", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(disposedField);
        return (bool)disposedField!.GetValue(httpApi)!;
    }

    private sealed class NullPlayerRuntimeBridge : IPlayerRuntimeBridge
    {
        public AkashaNavigator.Views.Windows.PlayerWindow? GetPlayerWindow() => null;

        public void SetPlayerWindow(AkashaNavigator.Views.Windows.PlayerWindow playerWindow)
        {
        }
    }

    private sealed class TrackingCursorDetectionService : ICursorDetectionService
    {
        private EventHandler? _cursorShown;
        private EventHandler? _cursorHidden;

        public int RemovedShownHandlers { get; private set; }

        public int RemovedHiddenHandlers { get; private set; }

        public event EventHandler? CursorShown
        {
            add => _cursorShown += value;
            remove
            {
                RemovedShownHandlers++;
                _cursorShown -= value;
            }
        }

        public event EventHandler? CursorHidden
        {
            add => _cursorHidden += value;
            remove
            {
                RemovedHiddenHandlers++;
                _cursorHidden -= value;
            }
        }

        public bool IsRunning { get; private set; }

        public bool IsCursorCurrentlyVisible => false;

        public string? TargetProcessName => null;

        public bool EnableDebugLog { get; set; }

        public bool IsSuspended { get; private set; }

        public void Start(string? targetProcessName = null, int intervalMs = 200, bool enableDebugLog = false)
        {
            IsRunning = true;
        }

        public void StartWithWhitelist(HashSet<string> whitelist, int intervalMs = 200, bool enableDebugLog = false)
        {
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
            IsSuspended = false;
        }

        public void Suspend()
        {
            IsSuspended = true;
        }

        public void Resume()
        {
            IsSuspended = false;
        }

        public void RefreshState()
        {
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
