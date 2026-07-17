using System;
using System.Collections.Generic;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public class ShutdownCoordinatorTests
{
    [Fact]
    public void Shutdown_ExecutesStagesInOrderOnlyOnce()
    {
        var coordinator = CreateCoordinator();
        var calls = new List<string>();
        coordinator.RegisterStage("third", 300, () => calls.Add("third"));
        coordinator.RegisterStage("first", 100, () => calls.Add("first"));
        coordinator.RegisterStage("second", 200, () => calls.Add("second"));

        coordinator.Shutdown();
        coordinator.Shutdown();

        Assert.Equal(new[] { "first", "second", "third" }, calls);
        Assert.True(coordinator.IsShutdownStarted);
    }

    [Fact]
    public void Shutdown_WhenStageFails_ContinuesWithRemainingStages()
    {
        var logService = new Mock<ILogService>();
        var coordinator = new ShutdownCoordinator(logService.Object);
        var calls = new List<string>();
        coordinator.RegisterStage("failing", 100, () => throw new InvalidOperationException("failure"));
        coordinator.RegisterStage("next", 200, () => calls.Add("next"));

        coordinator.Shutdown();

        Assert.Equal(new[] { "next" }, calls);
        logService.Verify(
            service => service.Error(
                nameof(ShutdownCoordinator),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<string>(),
                It.IsAny<object?[]>()),
            Times.Once);
    }

    [Fact]
    public void RegisterStage_AfterShutdownStarted_Throws()
    {
        var coordinator = CreateCoordinator();
        coordinator.Shutdown();

        Assert.Throws<InvalidOperationException>(
            () => coordinator.RegisterStage("late", 100, () => { }));
    }

    [Fact]
    public void RegisterStage_WithDuplicateName_Throws()
    {
        var coordinator = CreateCoordinator();
        coordinator.RegisterStage("duplicate", 100, () => { });

        Assert.Throws<InvalidOperationException>(
            () => coordinator.RegisterStage("duplicate", 200, () => { }));
    }

    private static ShutdownCoordinator CreateCoordinator()
    {
        return new ShutdownCoordinator(Mock.Of<ILogService>());
    }
}
