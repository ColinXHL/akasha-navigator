using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
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

/// <summary>
/// BossKeyHiddenModeChangedEvent 单元测试
/// 验证老板键隐藏模式事件的数据传递
/// </summary>
public class BossKeyHiddenModeChangedEventTests
{
    [Fact]
    public void BossKeyHiddenModeChangedEvent_IsHidden_True()
    {
        var e = new BossKeyHiddenModeChangedEvent { IsHidden = true };
        Assert.True(e.IsHidden);
    }

    [Fact]
    public void BossKeyHiddenModeChangedEvent_IsHidden_False()
    {
        var e = new BossKeyHiddenModeChangedEvent { IsHidden = false };
        Assert.False(e.IsHidden);
    }

    [Fact]
    public void BossKeyHiddenModeChangedEvent_Default_IsHidden_False()
    {
        var e = new BossKeyHiddenModeChangedEvent();
        Assert.False(e.IsHidden);
    }
}
