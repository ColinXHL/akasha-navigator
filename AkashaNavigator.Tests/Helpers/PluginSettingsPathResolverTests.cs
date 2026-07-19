using System.IO;
using System.Text.Json;
using AkashaNavigator.Helpers;
using Xunit;

namespace AkashaNavigator.Tests.Helpers;

public class PluginSettingsPathResolverTests
{
    [Fact]
    public void ResolveDirectory_WithRelativePath_StaysInsidePluginDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveDirectory(
            root, "worker/win-x64/Assets/Config/Pick");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "worker/win-x64/Assets/Config/Pick")),
            result);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("../../outside")]
    public void ResolveDirectory_WithTraversal_ReturnsNull(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveDirectory(root, relativePath);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveDirectory_WithoutRelativePath_ReturnsPluginDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveDirectory(root, null);

        Assert.Equal(Path.GetFullPath(root), result);
    }

    [Fact]
    public void ResolveSettingsFile_WithManifestPath_UsesDeclaredLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveSettingsFile(
            root, "frontend/settings_ui.json");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "frontend/settings_ui.json")),
            result);
    }

    [Fact]
    public void ResolveSettingsFile_WithoutManifestPath_UsesLegacyLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveSettingsFile(root, null);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, AppConstants.PluginSettingsUiFileName)),
            result);
    }

    [Fact]
    public void ResolveSettingsFile_WithInstalledRepositoryManifest_UsesCatalogLocation()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AkashaNavigator",
            Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, AppConstants.PluginRepositoryManifestFileName),
            JsonSerializer.Serialize(new {
                settings = "frontend/settings_ui.json"
            }));

        try
        {
            var result = PluginSettingsPathResolver.ResolveSettingsFile(root, null);

            Assert.Equal(
                Path.GetFullPath(Path.Combine(root, "frontend/settings_ui.json")),
                result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveSettingsFile_WithTraversal_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-root");

        var result = PluginSettingsPathResolver.ResolveSettingsFile(
            root, "../settings_ui.json");

        Assert.Null(result);
    }
}
