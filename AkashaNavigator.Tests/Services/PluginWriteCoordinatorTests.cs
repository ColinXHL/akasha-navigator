using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginWriteCoordinatorTests
{
    [Fact]
    public async Task AcquireAsync_WaitsUntilCurrentWriterReleases()
    {
        var coordinator = new PluginWriteCoordinator();
        using var firstWriter = coordinator.Acquire();

        var secondWriterTask = coordinator.AcquireAsync().AsTask();

        Assert.False(secondWriterTask.IsCompleted);
        firstWriter.Dispose();
        using var secondWriter = await secondWriterTask;
        Assert.True(secondWriterTask.IsCompletedSuccessfully);
    }
}
