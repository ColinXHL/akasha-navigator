using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Core;
using Microsoft.ClearScript.V8;
using Xunit;

namespace AkashaNavigator.Tests.Plugins;

public sealed class PluginPathSafetyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _pluginDirectory;

    public PluginPathSafetyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"AkashaNavigator.PluginPathSafety.{Guid.NewGuid():N}");
        _pluginDirectory = Path.Combine(_tempRoot, "plugin");

        Directory.CreateDirectory(_pluginDirectory);
    }

    [Fact]
    public void LoadScript_ShouldFail_WhenMainEscapesPluginDirectory()
    {
        var escapedMainRelativePath = Path.Combine("..", "outside", "evil.js");
        var escapedMainFullPath = Path.GetFullPath(Path.Combine(_pluginDirectory, escapedMainRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(escapedMainFullPath)!);
        File.WriteAllText(escapedMainFullPath, "function onLoad() {} function onUnload() {}");

        var manifest = new PluginManifest
        {
            Id = "plugin.escape-main",
            Name = "Escape Main",
            Version = "1.0.0",
            Main = escapedMainRelativePath
        };

        using var context = new PluginContext(manifest, _pluginDirectory);
        var loaded = context.LoadScript();

        Assert.False(loaded);
    }

    [Fact]
    public void BuildSearchPath_ShouldIgnore_LibraryPathOutsidePluginRoot()
    {
        var outsideLibraryRelativePath = Path.Combine("..", "outside-lib");
        var outsideLibraryFullPath = Path.GetFullPath(Path.Combine(_pluginDirectory, outsideLibraryRelativePath));
        Directory.CreateDirectory(outsideLibraryFullPath);

        var manifest = new PluginManifest
        {
            Id = "plugin.escape-library",
            Name = "Escape Library",
            Version = "1.0.0",
            Main = "main.js",
            Permissions = new List<string>()
        };

        using var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
        PluginEngine.InitializeEngine(engine,
                                      _pluginDirectory,
                                      _pluginDirectory,
                                      new[] { outsideLibraryRelativePath },
                                      new PluginConfig(manifest.Id),
                                      manifest,
                                      new PluginEngineOptions());

        var searchPaths = (engine.DocumentSettings.SearchPath ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.DoesNotContain(outsideLibraryFullPath, searchPaths, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }
}
