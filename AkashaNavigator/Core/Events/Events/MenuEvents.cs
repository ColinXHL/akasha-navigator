namespace AkashaNavigator.Core.Events.Events
{
/// <summary>
/// 菜单请求事件（通用）
/// </summary>
public class MenuRequestedEvent
{
    public MenuType MenuType { get; set; }
}

/// <summary>
/// 菜单类型
/// </summary>
public enum MenuType
{
    /// <summary>历史记录</summary>
    History,

    /// <summary>收藏夹</summary>
    Bookmarks,

    /// <summary>插件中心</summary>
    PluginCenter,

    /// <summary>设置</summary>
    Settings,

    /// <summary>开荒笔记</summary>
    PioneerNotes
}

/// <summary>
/// 收藏夹菜单请求事件
/// </summary>
public class BookmarksRequestedEvent
{
}

/// <summary>
/// 收藏夹项选中事件
/// </summary>
public class BookmarkItemSelectedEvent
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 插件中心请求事件
/// </summary>
public class PluginCenterRequestedEvent
{
}

/// <summary>
/// 设置请求事件
/// </summary>
public class SettingsRequestedEvent
{
}

/// <summary>
/// 开荒笔记请求事件
/// </summary>
public class PioneerNotesRequestedEvent
{
}

/// <summary>
/// 开荒笔记项选中事件
/// </summary>
public class PioneerNoteItemSelectedEvent
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Profile 列表变化事件（安装/卸载后触发）
/// </summary>
public class ProfileListChangedEvent
{
}

/// <summary>
/// 插件列表变化事件（安装/卸载后触发）
/// </summary>
public class PluginListChangedEvent
{
}
}
