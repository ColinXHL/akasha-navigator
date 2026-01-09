using System;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 播放速率控制器
/// 封装播放速率的计算逻辑，便于测试
/// </summary>
public class PlaybackRateController
{
    /// <summary>
    /// 播放速率最小值
    /// </summary>
    public const double MinRate = 0.25;

    /// <summary>
    /// 播放速率最大值
    /// </summary>
    public const double MaxRate = 4.0;

    /// <summary>
    /// 播放速率步进值
    /// </summary>
    public const double StepSize = 0.25;

    /// <summary>
    /// 默认播放速率
    /// </summary>
    public const double DefaultRate = 1.0;

    private double _currentRate = DefaultRate;

    /// <summary>
    /// 当前播放速率
    /// </summary>
    public double CurrentRate => _currentRate;

    /// <summary>
    /// 设置播放速率（自动限制在有效范围内）
    /// </summary>
    /// <param name="rate">目标速率</param>
    /// <returns>实际设置的速率</returns>
    public double SetRate(double rate)
    {
        _currentRate = Math.Clamp(rate, MinRate, MaxRate);
        return _currentRate;
    }

    /// <summary>
    /// 增加播放速率
    /// </summary>
    /// <returns>新的播放速率</returns>
    public double IncreaseRate()
    {
        return SetRate(_currentRate + StepSize);
    }

    /// <summary>
    /// 减少播放速率
    /// </summary>
    /// <returns>新的播放速率</returns>
    public double DecreaseRate()
    {
        return SetRate(_currentRate - StepSize);
    }

    /// <summary>
    /// 重置播放速率到默认值
    /// </summary>
    /// <returns>默认播放速率 (1.0)</returns>
    public double ResetRate()
    {
        _currentRate = DefaultRate;
        return _currentRate;
    }
}
}
