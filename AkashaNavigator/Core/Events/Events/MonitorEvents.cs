namespace AkashaNavigator.Core.Events.Events
{
/// <summary>
/// 显示器拓扑变化事件
/// 当系统显示设置发生变化时（如显示器连接/断开、分辨率改变、主显示器切换等）发布
/// </summary>
public class DisplayTopologyChangedEvent
{
}

/// <summary>
/// 播放器当前显示器变化事件
/// 当播放器窗口移动到不同显示器、启动时初始化显示器、最大化/还原后发布
/// </summary>
public class PlayerMonitorChangedEvent
{
    /// <summary>
    /// 播放器窗口当前所在的显示器信息
    /// </summary>
    public Helpers.MonitorInfo? MonitorInfo { get; set; }
}
}