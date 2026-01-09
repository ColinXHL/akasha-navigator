namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 设置窗口页面类型
/// </summary>
public enum SettingsPageType
{
    /// <summary>
    /// 通用设置页面（Profile 选择、基础设置、打开文件夹、插件中心）
    /// </summary>
    General,

    /// <summary>
    /// 窗口设置页面（边缘吸附、吸附阈值、退出提示）
    /// </summary>
    Window,

    /// <summary>
    /// 快捷键设置页面（全局控制、视频控制、透明度、窗口行为、播放速率、窗口控制）
    /// </summary>
    Hotkeys,

    /// <summary>
    /// 高级设置页面（插件更新提示、调试日志）
    /// </summary>
    Advanced
}
