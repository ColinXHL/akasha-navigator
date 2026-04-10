using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Microsoft.ClearScript.V8;
using Xunit;

namespace AkashaNavigator.Tests.Plugins;

public class PluginHostObjectFactoryTests
{
    [Fact]
    public void CreateWindowApi_UsesInjectedRuntimeBridgeAndCursorService()
    {
        var runtimeBridge = new TestPlayerRuntimeBridge();
        var cursorDetectionService = new TestCursorDetectionService();
        var factory = new PluginHostObjectFactory(runtimeBridge,
                                                  new OverlayManager(),
                                                  new PanelManager(),
                                                  cursorDetectionService,
                                                  new SubtitleService(new FakeLogService()),
                                                  new ScriptExecutionQueue(new FakeLogService()),
                                                  new HotkeyService(),
                                                  new OsdManager(),
                                                  new FakeLogService());

        var context = new PluginContext("plugin.alpha", @"C:\\plugin", @"C:\\config",
                                        new PluginManifest
                                        {
                                            Id = "plugin.alpha",
                                            Name = "Alpha",
                                            Version = "1.0.0",
                                            Main = "main.js"
                                        });
        var eventManager = new EventManager();

        var api = factory.CreateWindowApi(context, eventManager);

        Assert.NotNull(api);
        Assert.False(api.IsClickThrough());
    }

    [Fact]
    public void InitializeEngine_WithFactoryPath_ExposesPermissionApisAndOsdWithoutLegacyOsdManager()
    {
        using var tempDir = new TempDirectory();
        using var engine = CreateEngineForTest();

        var factory = new PluginHostObjectFactory(new TestPlayerRuntimeBridge(),
                                                  new OverlayManager(),
                                                  new PanelManager(),
                                                  new TestCursorDetectionService(),
                                                  new SubtitleService(new FakeLogService()),
                                                  new ScriptExecutionQueue(new FakeLogService()),
                                                  new HotkeyService(),
                                                  new OsdManager(),
                                                  new FakeLogService());

        PluginEngine.InitializeEngine(engine,
                                      tempDir.Path,
                                      tempDir.Path,
                                      null,
                                      new PluginConfig("plugin.alpha"),
                                      CreateManifest(PluginPermissions.Overlay, PluginPermissions.Player),
                                      new PluginEngineOptions
                                      {
                                          HostObjectFactory = factory
                                      });

        Assert.True(IsGlobalDefined(engine, "overlay"));
        Assert.True(IsGlobalDefined(engine, "player"));
        Assert.True(IsGlobalDefined(engine, "webview"));
        Assert.True(IsGlobalDefined(engine, "osd"));
    }

    [Fact]
    public void InitializeEngine_WithoutFactory_UsesLegacyDependenciesForApiExposure()
    {
        using var tempDir = new TempDirectory();
        using var engine = CreateEngineForTest();

        PluginEngine.InitializeEngine(engine,
                                      tempDir.Path,
                                      tempDir.Path,
                                      null,
                                      new PluginConfig("plugin.legacy"),
                                      CreateManifest(PluginPermissions.Overlay,
                                                     PluginPermissions.Player,
                                                     PluginPermissions.Window,
                                                     PluginPermissions.Panel,
                                                     PluginPermissions.Subtitle,
                                                     PluginPermissions.Hotkey),
#pragma warning disable CS0618
                                      new PluginEngineOptions
                                      {
                                          RuntimeBridge = new TestPlayerRuntimeBridge(),
                                          OverlayManager = new OverlayManager(),
                                          PanelManager = new PanelManager(),
                                          CursorDetectionService = new TestCursorDetectionService(),
                                          SubtitleService = new SubtitleService(new FakeLogService()),
                                          ScriptExecutionQueue = new ScriptExecutionQueue(new FakeLogService()),
                                          HotkeyService = new HotkeyService(),
                                          ActionDispatcher = new ActionDispatcher(),
                                          LogService = new FakeLogService(),
                                          OsdManager = new OsdManager()
                                      });
#pragma warning restore CS0618

        Assert.True(IsGlobalDefined(engine, "overlay"));
        Assert.True(IsGlobalDefined(engine, "player"));
        Assert.True(IsGlobalDefined(engine, "window"));
        Assert.True(IsGlobalDefined(engine, "panel"));
        Assert.True(IsGlobalDefined(engine, "subtitle"));
        Assert.True(IsGlobalDefined(engine, "hotkey"));
        Assert.True(IsGlobalDefined(engine, "webview"));
        Assert.True(IsGlobalDefined(engine, "osd"));
    }

    [Fact]
    public void InitializeEngine_WithoutFactoryAndWithoutLegacyOsdManager_DoesNotExposeOsd()
    {
        using var tempDir = new TempDirectory();
        using var engine = CreateEngineForTest();

        PluginEngine.InitializeEngine(engine,
                                      tempDir.Path,
                                      tempDir.Path,
                                      null,
                                      new PluginConfig("plugin.no-osd"),
                                      CreateManifest(),
                                      new PluginEngineOptions());

        Assert.False(IsGlobalDefined(engine, "osd"));
    }

    private static V8ScriptEngine CreateEngineForTest()
    {
        return new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
    }

    private static PluginManifest CreateManifest(params string[] permissions)
    {
        return new PluginManifest
        {
            Id = "plugin.alpha",
            Name = "Alpha",
            Version = "1.0.0",
            Main = "main.js",
            Permissions = new List<string>(permissions)
        };
    }

    private static bool IsGlobalDefined(V8ScriptEngine engine, string name)
    {
        return (bool)engine.Evaluate($"typeof {name} !== 'undefined'");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AkashaNavigator.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }

    private sealed class TestPlayerRuntimeBridge : IPlayerRuntimeBridge
    {
        public AkashaNavigator.Views.Windows.PlayerWindow? GetPlayerWindow() => null;

        public void SetPlayerWindow(AkashaNavigator.Views.Windows.PlayerWindow playerWindow)
        {
        }
    }

    private sealed class TestCursorDetectionService : ICursorDetectionService
    {
        public event EventHandler? CursorShown;

        public event EventHandler? CursorHidden;

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
