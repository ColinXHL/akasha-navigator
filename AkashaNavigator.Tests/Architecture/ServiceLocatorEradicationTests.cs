using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AkashaNavigator.Tests.Architecture;

public class ServiceLocatorEradicationTests
{
    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AkashaNavigator.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    [Fact]
    public void ApplicationCode_ExceptAppXaml_ShouldNotUseAppServices()
    {
        var root = GetRepositoryRoot();
        var appDir = Path.Combine(root, "AkashaNavigator");

        var offenders = Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith("App.xaml.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(p).Contains("App.Services", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0, $"Found App.Services usage:\n{string.Join("\n", offenders)}");
    }

    [Theory]
    [InlineData("AkashaNavigator/Services/DataService.cs")]
    [InlineData("AkashaNavigator/Services/PioneerNoteService.cs")]
    [InlineData("AkashaNavigator/Services/WindowStateService.cs")]
    [InlineData("AkashaNavigator/Services/PluginAssociationManager.cs")]
    [InlineData("AkashaNavigator/Services/ProfileMarketplaceService.cs")]
    public void TargetServices_ShouldNotContainStaticInstance(string relativePath)
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(path);

        Assert.DoesNotContain(" Instance", text);
        Assert.DoesNotContain("ResetInstance", text);
    }
}
