using System;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 点击穿透状态管理器
/// 管理手动和自动两种独立的点击穿透状态
/// </summary>
public class ClickThroughStateManager
{
    private bool _isManualClickThrough;
    private bool _isAutoClickThrough;

    /// <summary>
    /// 手动点击穿透状态
    /// </summary>
    public bool IsManualClickThrough => _isManualClickThrough;

    /// <summary>
    /// 自动点击穿透状态（插件控制）
    /// </summary>
    public bool IsAutoClickThrough => _isAutoClickThrough;

    /// <summary>
    /// 有效的点击穿透状态（手动 OR 自动）
    /// </summary>
    public bool IsEffectiveClickThrough => _isManualClickThrough || _isAutoClickThrough;

    /// <summary>
    /// 切换手动点击穿透状态
    /// </summary>
    /// <returns>新的手动点击穿透状态</returns>
    public bool ToggleManualClickThrough()
    {
        _isManualClickThrough = !_isManualClickThrough;
        return _isManualClickThrough;
    }

    /// <summary>
    /// 设置手动点击穿透状态
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetManualClickThrough(bool enabled)
    {
        _isManualClickThrough = enabled;
    }

    /// <summary>
    /// 设置自动点击穿透状态（由插件控制）
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetAutoClickThrough(bool enabled)
    {
        _isAutoClickThrough = enabled;
    }

    /// <summary>
    /// 重置自动点击穿透状态（插件卸载或禁用时调用）
    /// </summary>
    public void ResetAutoClickThrough()
    {
        _isAutoClickThrough = false;
    }

    /// <summary>
    /// 重置所有状态
    /// </summary>
    public void Reset()
    {
        _isManualClickThrough = false;
        _isAutoClickThrough = false;
    }
}
}
