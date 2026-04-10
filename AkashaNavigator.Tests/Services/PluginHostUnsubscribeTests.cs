using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginHostUnsubscribeTests
{
    [Fact]
    public void UnsubscribePlugin_ShouldNotDelete_BuiltInPluginDirectory()
    {
        var pluginId = $"plugin-builtin-{Guid.NewGuid():N}";
        var testBuiltInRoot = Path.Combine(AppPaths.BuiltInPluginsDirectory, "__tests__", nameof(PluginHostUnsubscribeTests),
                                           Guid.NewGuid().ToString("N"));
        var builtInPluginDirectory = Path.Combine(testBuiltInRoot, pluginId);
        Directory.CreateDirectory(builtInPluginDirectory);

        var host = CreateHost();
        try
        {
            var manifest = new PluginManifest
            {
                Id = pluginId,
                Name = "BuiltIn Test Plugin",
                Version = "1.0.0",
                Main = "main.js"
            };

            var context = new PluginContext(pluginId, builtInPluginDirectory, builtInPluginDirectory, manifest);
            AddLoadedPlugin(host, context);

            var result = host.UnsubscribePlugin(pluginId);

            Assert.True(result.IsSuccess);
            Assert.True(Directory.Exists(builtInPluginDirectory));
        }
        finally
        {
            host.Dispose();

            if (Directory.Exists(testBuiltInRoot))
            {
                Directory.Delete(testBuiltInRoot, recursive: true);
            }
        }
    }

    private static PluginHost CreateHost()
    {
        var logService = new FakeLogService();
        var associationManager = new FakePluginAssociationManager();
        var pluginLibrary = new FakePluginLibrary();
        var runtimeBridge = new AkashaNavigator.Tests.TestPlayerRuntimeBridge();
        var overlayManager = new OverlayManager();
        var panelManager = new PanelManager();
        var cursorDetectionService = new AkashaNavigator.Tests.MockCursorDetectionService();
        var subtitleService = new SubtitleService(logService);
        var scriptExecutionQueue = new ScriptExecutionQueue(logService);
        var hotkeyService = new HotkeyService();
        var osdManager = new OsdManager();
        var hostObjectFactory = new PluginHostObjectFactory(runtimeBridge,
                                                            overlayManager,
                                                            panelManager,
                                                            cursorDetectionService,
                                                            subtitleService,
                                                            scriptExecutionQueue,
                                                            hotkeyService,
                                                            osdManager,
                                                            logService);

        return new PluginHost(true,
                              logService,
                              associationManager,
                              pluginLibrary,
                              runtimeBridge,
                              overlayManager,
                              panelManager,
                              cursorDetectionService,
                              subtitleService,
                              scriptExecutionQueue,
                              hotkeyService,
                              osdManager,
                              hostObjectFactory);
    }

    private static void AddLoadedPlugin(PluginHost host, PluginContext context)
    {
        var loadedPluginsField = typeof(PluginHost)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .SingleOrDefault(f => typeof(IList).IsAssignableFrom(f.FieldType) &&
                                  f.Name.Contains("loaded", StringComparison.OrdinalIgnoreCase) &&
                                  f.Name.Contains("plugin", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(loadedPluginsField);

        var loadedPlugins = loadedPluginsField!.GetValue(host) as IList;
        Assert.NotNull(loadedPlugins);

        loadedPlugins!.Add(context);
    }
}
