using System.Collections.Generic;

namespace AkashaNavigator.Models.Config
{
/// <summary>
/// 全局鼠标检测透明度自动调整配置
/// 用于 ClipCursor API 检测游戏 UI 状态并自动调整播放器透明度
/// </summary>
public class GlobalCursorDetectionConfig
{
    /// <summary>
    /// 是否启用全局鼠标检测（总开关）
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 全局进程白名单（这些进程在所有 Profile 下都会启用检测）
    /// </summary>
    public List<string>? ProcessWhitelist { get; set; }

    /// <summary>
    /// UI 模式下的最低透明度（0.0-1.0）
    /// </summary>
    public double MinOpacity { get; set; } = 0.3;

    /// <summary>
    /// 检测间隔（毫秒）
    /// </summary>
    public int CheckIntervalMs { get; set; } = 200;

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    public bool EnableDebugLog { get; set; } = false;
}
}
