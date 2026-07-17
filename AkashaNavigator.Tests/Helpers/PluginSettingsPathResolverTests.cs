using System.IO;
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
}
