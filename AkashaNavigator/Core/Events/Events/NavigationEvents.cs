namespace AkashaNavigator.Core.Events.Events
{
/// <summary>
/// 导航请求事件
/// </summary>
public class NavigationRequestedEvent
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 导航控制事件（后退、前进、刷新）
/// </summary>
public class NavigationControlEvent
{
    public NavigationControlAction Action { get; set; }
}

/// <summary>
/// 导航控制动作类型
/// </summary>
public enum NavigationControlAction
{
    /// <summary>后退</summary>
    Back,

    /// <summary>前进</summary>
    Forward,

    /// <summary>刷新</summary>
    Refresh
}

/// <summary>
/// URL 变化事件
/// </summary>
public class UrlChangedEvent
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 导航状态变化事件
/// </summary>
public class NavigationStateChangedEvent
{
    public bool CanGoBack { get; set; }
    public bool CanGoForward { get; set; }
}

/// <summary>
/// 页面标题变化事件
/// </summary>
public class TitleChangedEvent
{
    public string Title { get; set; } = string.Empty;
}
}
