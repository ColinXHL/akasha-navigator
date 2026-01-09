using AkashaNavigator.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// ActionDispatcher 属性测试
/// </summary>
public class ActionDispatcherPropertyTests
{
    /// <summary>
    /// 所有新增动作名称常量
    /// </summary>
    private static readonly string[] NewActionNames =
        new[] { ActionDispatcher.ActionResetOpacity,           ActionDispatcher.ActionIncreasePlaybackRate,
                ActionDispatcher.ActionDecreasePlaybackRate,   ActionDispatcher.ActionResetPlaybackRate,
                ActionDispatcher.ActionToggleWindowVisibility, ActionDispatcher.ActionSuspendHotkeys };

    /// <summary>
    /// 所有内置动作名称常量
    /// </summary>
    private static readonly string[] AllBuiltinActionNames =
        new[] { ActionDispatcher.ActionSeekBackward,         ActionDispatcher.ActionSeekForward,
                ActionDispatcher.ActionTogglePlay,           ActionDispatcher.ActionDecreaseOpacity,
                ActionDispatcher.ActionIncreaseOpacity,      ActionDispatcher.ActionToggleClickThrough,
                ActionDispatcher.ActionToggleMaximize,       ActionDispatcher.ActionResetOpacity,
                ActionDispatcher.ActionIncreasePlaybackRate, ActionDispatcher.ActionDecreasePlaybackRate,
                ActionDispatcher.ActionResetPlaybackRate,    ActionDispatcher.ActionToggleWindowVisibility,
                ActionDispatcher.ActionSuspendHotkeys };

    /// <summary>
    /// **Feature: hotkey-expansion, Property 12: Action registration completeness**
    /// **Validates: Requirements 7.1, 7.2**
    ///
    /// *For any* new action name (ResetOpacity, IncreasePlaybackRate, etc.),
    /// the ActionDispatcher SHALL have a registered handler.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllNewActions_AreRegistered()
    {
        var dispatcher = new ActionDispatcher();

        // 对于每个新动作，验证它已被注册
        return Prop.ForAll(Gen.Elements(NewActionNames).ToArbitrary(),
                           actionName => dispatcher.IsActionRegistered(actionName));
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 12: Action registration completeness**
    /// **Validates: Requirements 7.1, 7.2**
    ///
    /// *For any* builtin action name, the ActionDispatcher SHALL have a registered handler.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllBuiltinActions_AreRegistered()
    {
        var dispatcher = new ActionDispatcher();

        return Prop.ForAll(Gen.Elements(AllBuiltinActionNames).ToArbitrary(),
                           actionName => dispatcher.IsActionRegistered(actionName));
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 12: Action registration completeness**
    /// **Validates: Requirements 7.1, 7.2**
    ///
    /// *For any* registered action, dispatching it SHALL return true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RegisteredActions_CanBeDispatched()
    {
        var dispatcher = new ActionDispatcher();

        return Prop.ForAll(Gen.Elements(AllBuiltinActionNames).ToArbitrary(),
                           actionName => dispatcher.Dispatch(actionName));
    }

    /// <summary>
    /// 验证所有新动作都已注册
    /// </summary>
    [Fact]
    public void NewActions_AllRegistered()
    {
        var dispatcher = new ActionDispatcher();

        foreach (var actionName in NewActionNames)
        {
            Assert.True(dispatcher.IsActionRegistered(actionName), $"Action '{actionName}' should be registered");
        }
    }

    /// <summary>
    /// 验证新动作可以被分发
    /// </summary>
    [Fact]
    public void NewActions_CanBeDispatched()
    {
        var dispatcher = new ActionDispatcher();

        foreach (var actionName in NewActionNames)
        {
            var result = dispatcher.Dispatch(actionName);
            Assert.True(result, $"Action '{actionName}' should be dispatchable");
        }
    }

    /// <summary>
    /// 验证动作名称大小写不敏感
    /// </summary>
    [Theory]
    [InlineData("resetopacity")]
    [InlineData("RESETOPACITY")]
    [InlineData("ResetOpacity")]
    [InlineData("increaseplaybackrate")]
    [InlineData("INCREASEPLAYBACKRATE")]
    [InlineData("suspendhotkeys")]
    [InlineData("SUSPENDHOTKEYS")]
    public void ActionNames_AreCaseInsensitive(string actionName)
    {
        var dispatcher = new ActionDispatcher();

        Assert.True(dispatcher.IsActionRegistered(actionName),
                    $"Action '{actionName}' should be registered (case-insensitive)");
        Assert.True(dispatcher.Dispatch(actionName),
                    $"Action '{actionName}' should be dispatchable (case-insensitive)");
    }

    /// <summary>
    /// 验证事件被正确触发
    /// </summary>
    [Fact]
    public void ResetOpacity_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.ResetOpacity += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionResetOpacity);

        Assert.True(eventFired);
    }

    /// <summary>
    /// 验证 IncreasePlaybackRate 事件被正确触发
    /// </summary>
    [Fact]
    public void IncreasePlaybackRate_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.IncreasePlaybackRate += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionIncreasePlaybackRate);

        Assert.True(eventFired);
    }

    /// <summary>
    /// 验证 DecreasePlaybackRate 事件被正确触发
    /// </summary>
    [Fact]
    public void DecreasePlaybackRate_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.DecreasePlaybackRate += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionDecreasePlaybackRate);

        Assert.True(eventFired);
    }

    /// <summary>
    /// 验证 ResetPlaybackRate 事件被正确触发
    /// </summary>
    [Fact]
    public void ResetPlaybackRate_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.ResetPlaybackRate += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionResetPlaybackRate);

        Assert.True(eventFired);
    }

    /// <summary>
    /// 验证 ToggleWindowVisibility 事件被正确触发
    /// </summary>
    [Fact]
    public void ToggleWindowVisibility_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.ToggleWindowVisibility += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionToggleWindowVisibility);

        Assert.True(eventFired);
    }

    /// <summary>
    /// 验证 SuspendHotkeys 事件被正确触发
    /// </summary>
    [Fact]
    public void SuspendHotkeys_Event_IsFired()
    {
        var dispatcher = new ActionDispatcher();
        var eventFired = false;
        dispatcher.SuspendHotkeys += (s, e) => eventFired = true;

        dispatcher.Dispatch(ActionDispatcher.ActionSuspendHotkeys);

        Assert.True(eventFired);
    }
}
}
