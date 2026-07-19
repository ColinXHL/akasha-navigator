using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Pages.Settings;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class GeneralSettingsPageViewModelTests
{
    [Fact]
    public void ReleaseEventSubscriptions_UnsubscribesOnlyOnce()
    {
        var eventBus = new Mock<IEventBus>();
        var viewModel = CreateViewModel(eventBus.Object);

        viewModel.ReleaseEventSubscriptions();
        viewModel.ReleaseEventSubscriptions();

        eventBus.Verify(
            bus => bus.Unsubscribe<OpacityChangedEvent>(
                It.IsAny<Action<OpacityChangedEvent>>()),
            Times.Once);
    }

    [Fact]
    public void ReleaseEventSubscriptions_StopsFutureOpacityUpdates()
    {
        var eventBus = new EventBus();
        var viewModel = CreateViewModel(eventBus);
        eventBus.Publish(
            new OpacityChangedEvent {
                Opacity = 0.5,
                Source = OpacityChangeSource.Hotkey
            });
        Assert.Equal(50, viewModel.OpacityPercent);

        viewModel.ReleaseEventSubscriptions();
        eventBus.Publish(
            new OpacityChangedEvent {
                Opacity = 0.75,
                Source = OpacityChangeSource.Hotkey
            });

        Assert.Equal(50, viewModel.OpacityPercent);
    }

    private static GeneralSettingsPageViewModel CreateViewModel(
        IEventBus eventBus)
    {
        var configService = new Mock<IConfigService>();
        configService
            .SetupGet(service => service.Config)
            .Returns(new AppConfig());
        var profile = new GameProfile();
        var profileManager = new Mock<IProfileManager>();
        profileManager
            .SetupGet(service => service.InstalledProfiles)
            .Returns(new[] { profile });
        profileManager
            .SetupGet(service => service.CurrentProfile)
            .Returns(profile);

        return new GeneralSettingsPageViewModel(
            configService.Object,
            profileManager.Object,
            Mock.Of<IWindowStateService>(),
            eventBus);
    }
}
