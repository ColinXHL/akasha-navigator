namespace AkashaNavigator.Core.Events.Events
{
/// <summary>
/// 透明度变化事件
/// 用于设置界面和 PlayerWindow 之间的双向同步
/// </summary>
public class OpacityChangedEvent
{
    /// <summary>
    /// 透明度值 (0.0-1.0)
    /// </summary>
    public double Opacity { get; set; }

    /// <summary>
    /// 事件来源（用于避免循环触发）
    /// </summary>
    public OpacityChangeSource Source { get; set; }
}

/// <summary>
/// 透明度变化来源
/// </summary>
public enum OpacityChangeSource
{
    /// <summary>来自快捷键操作</summary>
    Hotkey,

    /// <summary>来自设置界面</summary>
    Settings,

    /// <summary>来自鼠标检测</summary>
    CursorDetection
}

/// <summary>
/// 请求获取当前透明度事件
/// </summary>
public class OpacityQueryEvent
{
    /// <summary>
    /// 回调：设置当前透明度值
    /// </summary>
    public System.Action<double>? Callback { get; set; }
}
}
