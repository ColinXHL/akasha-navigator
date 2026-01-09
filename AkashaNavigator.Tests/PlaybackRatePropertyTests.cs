using AkashaNavigator.Helpers;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// 播放速率属性测试
/// </summary>
public class PlaybackRatePropertyTests
{
    /// <summary>
    /// **Feature: hotkey-expansion, Property 5: Playback rate bounds**
    /// **Validates: Requirements 3.1, 3.2, 3.4, 3.5**
    ///
    /// *For any* playback rate adjustment, the resulting rate SHALL be within [0.25, 4.0] inclusive.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_SetRate_AlwaysWithinBounds(double inputRate)
    {
        // 过滤掉 NaN 和 Infinity - 这些不是有效的播放速率输入
        if (double.IsNaN(inputRate) || double.IsInfinity(inputRate))
            return true.ToProperty();

        var controller = new PlaybackRateController();
        var resultRate = controller.SetRate(inputRate);

        // 属性：结果速率始终在 [0.25, 4.0] 范围内
        return (resultRate >= PlaybackRateController.MinRate && resultRate <= PlaybackRateController.MaxRate)
            .ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 5: Playback rate bounds**
    /// **Validates: Requirements 3.4, 3.5**
    ///
    /// 验证边界值：最大值不会超过 4.0，最小值不会低于 0.25
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_IncreaseFromMax_StaysAtMax(int increaseCount)
    {
        // 限制增加次数为正数
        increaseCount = Math.Abs(increaseCount % 100) + 1;

        var controller = new PlaybackRateController();
        // 先设置到最大值
        controller.SetRate(PlaybackRateController.MaxRate);

        // 多次增加
        for (int i = 0; i < increaseCount; i++)
        {
            controller.IncreaseRate();
        }

        // 属性：速率应该保持在最大值
        return (controller.CurrentRate == PlaybackRateController.MaxRate).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 5: Playback rate bounds**
    /// **Validates: Requirements 3.4, 3.5**
    ///
    /// 验证边界值：从最小值减少仍保持最小值
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_DecreaseFromMin_StaysAtMin(int decreaseCount)
    {
        // 限制减少次数为正数
        decreaseCount = Math.Abs(decreaseCount % 100) + 1;

        var controller = new PlaybackRateController();
        // 先设置到最小值
        controller.SetRate(PlaybackRateController.MinRate);

        // 多次减少
        for (int i = 0; i < decreaseCount; i++)
        {
            controller.DecreaseRate();
        }

        // 属性：速率应该保持在最小值
        return (controller.CurrentRate == PlaybackRateController.MinRate).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 6: Playback rate step size**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// *For any* increase or decrease playback rate action, the rate SHALL change by exactly 0.25x (unless at bounds).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_Increase_ChangesBy025(double startRate)
    {
        // 过滤掉 NaN 和 Infinity
        if (double.IsNaN(startRate) || double.IsInfinity(startRate))
            return true.ToProperty();

        // 限制起始速率在有效范围内，且不在最大值
        startRate = Math.Clamp(startRate, PlaybackRateController.MinRate,
                               PlaybackRateController.MaxRate - PlaybackRateController.StepSize);

        var controller = new PlaybackRateController();
        controller.SetRate(startRate);
        var rateBefore = controller.CurrentRate;

        controller.IncreaseRate();
        var rateAfter = controller.CurrentRate;

        // 属性：如果不在边界，增加后应该正好增加 0.25
        var expectedRate = Math.Min(rateBefore + PlaybackRateController.StepSize, PlaybackRateController.MaxRate);
        return (Math.Abs(rateAfter - expectedRate) < 0.001).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 6: Playback rate step size**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// 验证减少操作的步进
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_Decrease_ChangesBy025(double startRate)
    {
        // 过滤掉 NaN 和 Infinity
        if (double.IsNaN(startRate) || double.IsInfinity(startRate))
            return true.ToProperty();

        // 限制起始速率在有效范围内，且不在最小值
        startRate = Math.Clamp(startRate, PlaybackRateController.MinRate + PlaybackRateController.StepSize,
                               PlaybackRateController.MaxRate);

        var controller = new PlaybackRateController();
        controller.SetRate(startRate);
        var rateBefore = controller.CurrentRate;

        controller.DecreaseRate();
        var rateAfter = controller.CurrentRate;

        // 属性：如果不在边界，减少后应该正好减少 0.25
        var expectedRate = Math.Max(rateBefore - PlaybackRateController.StepSize, PlaybackRateController.MinRate);
        return (Math.Abs(rateAfter - expectedRate) < 0.001).ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 7: Reset playback rate**
    /// **Validates: Requirements 4.1**
    ///
    /// *For any* starting playback rate, after reset action, the rate SHALL be exactly 1.0x.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_Reset_AlwaysReturnsOne(double startRate)
    {
        // 过滤掉 NaN 和 Infinity
        if (double.IsNaN(startRate) || double.IsInfinity(startRate))
            return true.ToProperty();

        var controller = new PlaybackRateController();
        controller.SetRate(startRate);

        var resetRate = controller.ResetRate();

        // 属性：重置后速率始终为 1.0
        return (resetRate == PlaybackRateController.DefaultRate &&
                controller.CurrentRate == PlaybackRateController.DefaultRate)
            .ToProperty();
    }

    /// <summary>
    /// **Feature: hotkey-expansion, Property 7: Reset playback rate**
    /// **Validates: Requirements 4.1**
    ///
    /// 验证多次操作后重置仍然有效
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaybackRate_Reset_AfterMultipleOperations(int operationCount)
    {
        operationCount = Math.Abs(operationCount % 50) + 1;

        var controller = new PlaybackRateController();
        var random = new System.Random(operationCount);

        // 执行随机操作
        for (int i = 0; i < operationCount; i++)
        {
            switch (random.Next(3))
            {
            case 0:
                controller.IncreaseRate();
                break;
            case 1:
                controller.DecreaseRate();
                break;
            case 2:
                controller.SetRate(random.NextDouble() * 5); // 随机设置
                break;
            }
        }

        // 重置
        controller.ResetRate();

        // 属性：无论之前做了什么操作，重置后速率都是 1.0
        return (controller.CurrentRate == PlaybackRateController.DefaultRate).ToProperty();
    }
}
}
