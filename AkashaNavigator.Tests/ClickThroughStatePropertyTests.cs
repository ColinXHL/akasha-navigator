using AkashaNavigator.Helpers;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// 点击穿透状态属性测试
/// </summary>
public class ClickThroughStatePropertyTests
{
    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 1: 双状态点击穿透独立性**
    /// **Validates: Requirements 1.10, 6.8, 7.1, 7.4**
    ///
    /// *For any* sequence of manual click-through toggles and auto click-through state changes,
    /// the manual state and auto state SHALL remain independent - changing one SHALL NOT affect the other.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_ManualAndAutoStates_AreIndependent(bool[] manualToggles, bool[] autoStates)
    {
        // 过滤空数组
        if (manualToggles == null || manualToggles.Length == 0)
            return true.ToProperty();
        if (autoStates == null || autoStates.Length == 0)
            return true.ToProperty();

        var manager = new ClickThroughStateManager();

        // 交替执行手动切换和自动状态设置
        int maxOps = Math.Max(manualToggles.Length, autoStates.Length);
        bool expectedManual = false;
        bool expectedAuto = false;

        for (int i = 0; i < maxOps; i++)
        {
            // 执行手动切换
            if (i < manualToggles.Length && manualToggles[i])
            {
                manager.ToggleManualClickThrough();
                expectedManual = !expectedManual;
            }

            // 验证自动状态未被影响
            if (manager.IsAutoClickThrough != expectedAuto)
                return false.ToProperty();

            // 执行自动状态设置
            if (i < autoStates.Length)
            {
                manager.SetAutoClickThrough(autoStates[i]);
                expectedAuto = autoStates[i];
            }

            // 验证手动状态未被影响
            if (manager.IsManualClickThrough != expectedManual)
                return false.ToProperty();
        }

        // 最终验证两个状态都正确
        return (manager.IsManualClickThrough == expectedManual && manager.IsAutoClickThrough == expectedAuto)
            .ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 1: 双状态点击穿透独立性**
    /// **Validates: Requirements 1.10, 6.8, 7.1, 7.4**
    ///
    /// 验证设置自动状态不影响手动状态
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_SetAutoState_DoesNotAffectManualState(bool initialManual, bool autoState)
    {
        var manager = new ClickThroughStateManager();

        // 设置初始手动状态
        manager.SetManualClickThrough(initialManual);

        // 设置自动状态
        manager.SetAutoClickThrough(autoState);

        // 属性：手动状态应该保持不变
        return (manager.IsManualClickThrough == initialManual).ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 1: 双状态点击穿透独立性**
    /// **Validates: Requirements 1.10, 6.8, 7.1, 7.4**
    ///
    /// 验证切换手动状态不影响自动状态
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_ToggleManualState_DoesNotAffectAutoState(bool initialAuto, int toggleCount)
    {
        toggleCount = Math.Abs(toggleCount % 50);

        var manager = new ClickThroughStateManager();

        // 设置初始自动状态
        manager.SetAutoClickThrough(initialAuto);

        // 多次切换手动状态
        for (int i = 0; i < toggleCount; i++)
        {
            manager.ToggleManualClickThrough();
        }

        // 属性：自动状态应该保持不变
        return (manager.IsAutoClickThrough == initialAuto).ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 2: 有效点击穿透 OR 逻辑**
    /// **Validates: Requirements 7.2, 7.3, 7.5**
    ///
    /// *For any* combination of manual click-through state and auto click-through state,
    /// the effective click-through state SHALL equal manual OR auto.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_EffectiveState_IsOrOfBothStates(bool manualState, bool autoState)
    {
        var manager = new ClickThroughStateManager();

        manager.SetManualClickThrough(manualState);
        manager.SetAutoClickThrough(autoState);

        // 属性：有效状态 = 手动 OR 自动
        bool expectedEffective = manualState || autoState;
        return (manager.IsEffectiveClickThrough == expectedEffective).ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 2: 有效点击穿透 OR 逻辑**
    /// **Validates: Requirements 7.2, 7.3, 7.5**
    ///
    /// 验证所有四种状态组合的 OR 逻辑
    /// </summary>
    [Fact]
    public void ClickThrough_EffectiveState_AllCombinations()
    {
        var manager = new ClickThroughStateManager();

        // (false, false) → false
        manager.SetManualClickThrough(false);
        manager.SetAutoClickThrough(false);
        Assert.False(manager.IsEffectiveClickThrough);

        // (true, false) → true
        manager.SetManualClickThrough(true);
        manager.SetAutoClickThrough(false);
        Assert.True(manager.IsEffectiveClickThrough);

        // (false, true) → true
        manager.SetManualClickThrough(false);
        manager.SetAutoClickThrough(true);
        Assert.True(manager.IsEffectiveClickThrough);

        // (true, true) → true
        manager.SetManualClickThrough(true);
        manager.SetAutoClickThrough(true);
        Assert.True(manager.IsEffectiveClickThrough);
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 3: 自动穿透状态清理**
    /// **Validates: Requirements 7.6**
    ///
    /// *For any* state where auto click-through is enabled, calling ResetAutoClickThrough()
    /// SHALL set auto state to false and effective state SHALL return to manual state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_ResetAuto_RestoresToManualState(bool manualState)
    {
        var manager = new ClickThroughStateManager();

        // 设置手动状态
        manager.SetManualClickThrough(manualState);

        // 启用自动穿透
        manager.SetAutoClickThrough(true);

        // 此时有效状态应该为 true（因为自动穿透启用）
        Assert.True(manager.IsEffectiveClickThrough);

        // 重置自动穿透
        manager.ResetAutoClickThrough();

        // 属性：自动状态应该为 false，有效状态应该等于手动状态
        return (manager.IsAutoClickThrough == false && manager.IsEffectiveClickThrough == manualState).ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 3: 自动穿透状态清理**
    /// **Validates: Requirements 7.6**
    ///
    /// 验证重置自动状态不影响手动状态
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThrough_ResetAuto_DoesNotAffectManualState(bool manualState, bool autoState)
    {
        var manager = new ClickThroughStateManager();

        manager.SetManualClickThrough(manualState);
        manager.SetAutoClickThrough(autoState);

        // 重置自动穿透
        manager.ResetAutoClickThrough();

        // 属性：手动状态应该保持不变
        return (manager.IsManualClickThrough == manualState).ToProperty();
    }
}
}
