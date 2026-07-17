using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_UsesSharedManifestService()
    {
        var manifest = new UpdateManifest {
            Stable = new AppUpdateChannelInfo {
                Version = "999.0.0",
                Notes = "shared manifest"
            }
        };
        var manifestService = new Mock<IUpdateManifestService>();
        manifestService
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateManifest>.Success(manifest));
        var service = new AppUpdateService(new Mock<ILogService>().Object, manifestService.Object);

        var result = await service.CheckForUpdateAsync(includePrerelease: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.HasUpdate);
        Assert.Equal("999.0.0", result.Value?.TargetVersion);
        Assert.Equal("shared manifest", result.Value?.Notes);
        manifestService.Verify(
            service => service.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenManifestRefreshFails_PropagatesFailure()
    {
        var expectedError = Error.Network("TEST_MANIFEST_FAILURE", "offline");
        var manifestService = new Mock<IUpdateManifestService>();
        manifestService
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateManifest>.Failure(expectedError));
        var service = new AppUpdateService(new Mock<ILogService>().Object, manifestService.Object);

        var result = await service.CheckForUpdateAsync(includePrerelease: false);

        Assert.True(result.IsFailure);
        Assert.Same(expectedError, result.Error);
    }
}
