using System.Reflection;
using Xunit;

namespace AkashaNavigator.Tests;

public sealed class AppConstantsVersionTests
{
    [Fact]
    public void Version_MatchesApplicationAssemblyInformationalVersion()
    {
        var informational = typeof(AppConstants).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var metadataIndex = informational.IndexOf('+');
        var expected = metadataIndex > 0
            ? informational[..metadataIndex]
            : informational;

        Assert.Equal(expected, AppConstants.Version);
    }
}
