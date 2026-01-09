using AkashaNavigator.Helpers;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// 窗口可见性属性测试
/// </summary>
public class WindowVisibilityPropertyTests
{
    /// <summary>
    /// **Feature: hotkey-expansion, Property 8: Window visibility toggle**
    /// **Validates: Requirements 5.1, 5.2**
    ///
    /// *For any* toggle visibility action, the visibility state SHALL flip between visible and hidden.
    /// </summary>
    [Property(MaxTest = 100)]
    public void WindowVisibility_Toggle_FlipsState()
    {
        // 创建一个新的控制器实例
        var controller = new WindowVisibilityController();

        // 初始状态应该是可见的（不是隐藏的）
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsHidden);

        // 第一次切换：应该变为隐藏
        var isVisibleAfterFirstToggle = controller.ToggleVisibility();
        Assert.False(isVisibleAfterFirstToggle);
        Assert.True(controller.IsHidden);
        Assert.False(controller.IsVisible);

        // 第二次切换：应该恢复可见
        var isVisibleAfterSecondToggle = controller.ToggleVisibility();
        Assert.True(isVisibleAfterSecondToggle);
        Assert.False(controller.IsHidden);
        Assert.True(controller.IsVisible);
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 8: Window visibility toggle**
    /// **Validates: Requirements 5.1, 5.2**
    ///
    /// 验证多次切换后状态一致
    /// </summary>
    [Property(MaxTest = 100)]
    public void WindowVisibility_MultipleToggles_AlternatesState(int toggleCount)
    {
        // 限制切换次数为非负数
        toggleCount = Math.Abs(toggleCount % 100);

        var controller = new WindowVisibilityController();

        // 记录预期的最终状态
        // 偶数次切换后应该是可见的，奇数次切换后应该是隐藏的
        bool shouldBeVisible = toggleCount % 2 == 0;

        // 执行切换
        for (int i = 0; i < toggleCount; i++)
        {
            controller.ToggleVisibility();
        }

        // 属性：切换次数为偶数时可见，奇数时隐藏
        Assert.Equal(shouldBeVisible, controller.IsVisible);
        Assert.Equal(!shouldBeVisible, controller.IsHidden);
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 8: Window visibility toggle**
    /// **Validates: Requirements 5.1, 5.2**
    ///
    /// 验证 Show() 和 Hide() 方法的行为
    /// </summary>
    [Property(MaxTest = 100)]
    public void WindowVisibility_ShowHide_SetsCorrectState()
    {
        var controller = new WindowVisibilityController();

        // 初始状态应该是可见的
        Assert.True(controller.IsVisible);

        // 调用 Hide()
        controller.Hide();
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsHidden);

        // 再次调用 Hide() 应该保持隐藏状态
        controller.Hide();
        Assert.False(controller.IsVisible);
        Assert.True(controller.IsHidden);

        // 调用 Show()
        controller.Show();
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsHidden);

        // 再次调用 Show() 应该保持显示状态
        controller.Show();
        Assert.True(controller.IsVisible);
        Assert.False(controller.IsHidden);
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 8: Window visibility toggle**
    /// **Validates: Requirements 5.1, 5.2**
    ///
    /// 验证 Show/Hide 与 Toggle 的交互
    /// </summary>
    [Property(MaxTest = 100)]
    public void WindowVisibility_ShowHideToggle_Interaction()
    {
        var controller = new WindowVisibilityController();

        // 隐藏
        controller.Hide();
        Assert.True(controller.IsHidden);

        // 从隐藏状态切换 -> 显示
        controller.ToggleVisibility();
        Assert.True(controller.IsVisible);

        // 从显示状态切换 -> 隐藏
        controller.ToggleVisibility();
        Assert.True(controller.IsHidden);

        // 显示
        controller.Show();
        Assert.True(controller.IsVisible);

        // 从显示状态切换 -> 隐藏
        controller.ToggleVisibility();
        Assert.True(controller.IsHidden);
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 8: Window visibility toggle**
    /// **Validates: Requirements 5.1, 5.2**
    ///
    /// ToggleVisibility 返回值应该反映新的可见性状态
    /// </summary>
    [Property(MaxTest = 100)]
    public void WindowVisibility_ToggleReturnValue_MatchesIsVisible(int toggleCount)
    {
        toggleCount = Math.Abs(toggleCount % 100);

        var controller = new WindowVisibilityController();

        bool? lastReturnValue = null;
        for (int i = 0; i < toggleCount; i++)
        {
            lastReturnValue = controller.ToggleVisibility();
            // 属性：返回值应该与 IsVisible 一致
            Assert.Equal(lastReturnValue, controller.IsVisible);
        }

        if (toggleCount > 0 && lastReturnValue.HasValue)
        {
            Assert.Equal(lastReturnValue.Value, controller.IsVisible);
        }
    }
}
}
