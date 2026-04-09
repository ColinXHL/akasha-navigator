using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;
using Xunit;

namespace AkashaNavigator.Tests.Windows;

public class ControlBarDisplayControllerTests
{
    [Fact]
    public void EvaluateMouse_WhenPointerLeavesWindowAndDelayExpires_ReturnsHidden()
    {
        var controller = new ControlBarDisplayController();
        controller.SetState(ControlBarDisplayState.Expanded, DateTime.UtcNow.AddSeconds(-2));

        var result = controller.EvaluateHideDelay(
            isMouseOverWindow: false,
            isMouseInTopTriggerZone: false,
            isContextMenuOpen: false,
            isUrlTextBoxFocused: false,
            nowUtc: DateTime.UtcNow);

        Assert.Equal(ControlBarDisplayState.Hidden, result.NextState);
    }
}
