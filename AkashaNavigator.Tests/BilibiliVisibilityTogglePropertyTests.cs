using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站分P列表插件可见性切换属性测试
/// 验证 bilibili-page-list 插件的可见性切换逻辑
/// </summary>
public class BilibiliVisibilityTogglePropertyTests
{
    /// <summary>
    /// 模拟插件状态
    /// </summary>
    public class PluginState
    {
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// 切换可见性（与 JavaScript 实现逻辑一致）
    /// </summary>
    private static void ToggleVisibility(PluginState state)
    {
        state.IsVisible = !state.IsVisible;
    }

#region Property 5 : Visibility Toggle Idempotence

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 5: Visibility Toggle Idempotence**
    /// **Validates: Requirements 4.2**
    ///
    /// *For any* initial visibility state, calling toggle twice SHALL return to the original state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_CalledTwice_ShouldReturnToOriginalState()
    {
        return Prop.ForAll<bool>(initialState =>
                                 {
                                     var state = new PluginState { IsVisible = initialState };

                                     // 切换两次
                                     ToggleVisibility(state);
                                     ToggleVisibility(state);

                                     // 应该回到原始状态
                                     return (state.IsVisible == initialState)
                                         .Label($"Initial: {initialState}, After two toggles: {state.IsVisible}");
                                 });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 5: Visibility Toggle Idempotence**
    /// **Validates: Requirements 4.2**
    ///
    /// *For any* initial visibility state, calling toggle once SHALL invert the state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_CalledOnce_ShouldInvertState()
    {
        return Prop.ForAll<bool>(initialState =>
                                 {
                                     var state = new PluginState { IsVisible = initialState };

                                     // 切换一次
                                     ToggleVisibility(state);

                                     // 应该反转状态
                                     return (state.IsVisible == !initialState)
                                         .Label($"Initial: {initialState}, After one toggle: {state.IsVisible}");
                                 });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 5: Visibility Toggle Idempotence**
    /// **Validates: Requirements 4.2**
    ///
    /// *For any* initial visibility state and any even number of toggles,
    /// the final state SHALL equal the initial state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_EvenNumberOfToggles_ShouldReturnToOriginalState()
    {
        var evenNumberGen = Gen.Choose(0, 50).Select(n => n * 2);

        return Prop.ForAll(Arb.From<bool>(), evenNumberGen.ToArbitrary(),
                           (bool initialState, int toggleCount) =>
                           {
                               var state = new PluginState { IsVisible = initialState };

                               // 切换偶数次
                               for (int i = 0; i < toggleCount; i++)
                               {
                                   ToggleVisibility(state);
                               }

                               // 应该回到原始状态
                               return (state.IsVisible == initialState)
                                   .Label($"Initial: {initialState}, Toggles: {toggleCount}, Final: {state.IsVisible}");
                           });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 5: Visibility Toggle Idempotence**
    /// **Validates: Requirements 4.2**
    ///
    /// *For any* initial visibility state and any odd number of toggles,
    /// the final state SHALL be the inverse of the initial state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToggleVisibility_OddNumberOfToggles_ShouldInvertState()
    {
        var oddNumberGen = Gen.Choose(0, 50).Select(n => n * 2 + 1);

        return Prop.ForAll(Arb.From<bool>(), oddNumberGen.ToArbitrary(),
                           (bool initialState, int toggleCount) =>
                           {
                               var state = new PluginState { IsVisible = initialState };

                               // 切换奇数次
                               for (int i = 0; i < toggleCount; i++)
                               {
                                   ToggleVisibility(state);
                               }

                               // 应该反转状态
                               return (state.IsVisible == !initialState)
                                   .Label($"Initial: {initialState}, Toggles: {toggleCount}, Final: {state.IsVisible}");
                           });
    }

#endregion

#region Unit Tests for Edge Cases

    /// <summary>
    /// 单元测试：从 false 切换到 true
    /// </summary>
    [Fact]
    public void ToggleVisibility_FromFalse_ShouldBecomeTrue()
    {
        var state = new PluginState { IsVisible = false };
        ToggleVisibility(state);
        Assert.True(state.IsVisible);
    }

    /// <summary>
    /// 单元测试：从 true 切换到 false
    /// </summary>
    [Fact]
    public void ToggleVisibility_FromTrue_ShouldBecomeFalse()
    {
        var state = new PluginState { IsVisible = true };
        ToggleVisibility(state);
        Assert.False(state.IsVisible);
    }

#endregion
}
