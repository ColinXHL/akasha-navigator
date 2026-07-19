using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginVersionTests
{
    [Theory]
    [InlineData("1.3.0", "1.3.0-alpha.4")]
    [InlineData("1.3.0-alpha.4", "1.3.0-alpha.3")]
    [InlineData("0.3.2", "0.3.1")]
    [InlineData("1.0.0-beta.11", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta", "1.0.0-alpha.9")]
    public void CompareVersions_ShouldOrderHigherSemanticVersionFirst(string higher, string lower)
    {
        Assert.True(PluginLibrary.CompareVersions(higher, lower) > 0);
        Assert.True(PluginLibrary.CompareVersions(lower, higher) < 0);
    }

    [Theory]
    [InlineData("1.3.0+build.2", "1.3.0+build.1")]
    [InlineData("v0.3.2", "0.3.2")]
    [InlineData("1.2", "1.2.0")]
    public void CompareVersions_ShouldTreatEquivalentSemanticVersionsAsEqual(string left, string right)
    {
        Assert.Equal(0, PluginLibrary.CompareVersions(left, right));
    }

    [Theory]
    [InlineData("1.4.0-alpha.2", "1.4.0")]
    [InlineData("1.4.0-beta.1", "1.4.0")]
    [InlineData("1.4.0", "1.4.0")]
    [InlineData("1.4.1-alpha.1", "1.4.0")]
    [InlineData("1.4.0-alpha.2", "1.4.0-alpha.1")]
    public void IsHostVersionCompatible_ShouldAcceptSupportedHost(
        string current,
        string minimum)
    {
        Assert.True(PluginLibrary.IsHostVersionCompatible(current, minimum));
    }

    [Theory]
    [InlineData("1.3.9", "1.4.0")]
    [InlineData("1.4.0-alpha.1", "1.4.0-alpha.2")]
    [InlineData("1.4.0-alpha.2", "1.4.1")]
    public void IsHostVersionCompatible_ShouldRejectUnsupportedHost(
        string current,
        string minimum)
    {
        Assert.False(PluginLibrary.IsHostVersionCompatible(current, minimum));
    }
}
