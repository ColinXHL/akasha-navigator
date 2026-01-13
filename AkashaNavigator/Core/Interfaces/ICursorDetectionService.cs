using System;
using System.Collections.Generic;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// 鼠标光标检测服务接口
/// 用于检测游戏内鼠标是否显示（如打开地图/菜单时）
/// </summary>
public interface ICursorDetectionService : IDisposable
{
    /// <summary>
    /// 鼠标从隐藏变为显示时触发
    /// </summary>
    event EventHandler? CursorShown;

    /// <summary>
    /// 鼠标从显示变为隐藏时触发
    /// </summary>
    event EventHandler? CursorHidden;

    /// <summary>
    /// 是否正在运行检测
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 当前鼠标是否可见
    /// </summary>
    bool IsCursorCurrentlyVisible { get; }

    /// <summary>
    /// 目标进程名
    /// </summary>
    string? TargetProcessName { get; }

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    bool EnableDebugLog { get; set; }

    /// <summary>
    /// 启动鼠标检测
    /// </summary>
    /// <param name="targetProcessName">目标进程名（不含扩展名），仅当此进程在前台时检测</param>
    /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
    /// <param name="enableDebugLog">是否启用调试日志，默认 false</param>
    void Start(string? targetProcessName = null, int intervalMs = 200, bool enableDebugLog = false);

    /// <summary>
    /// 启动鼠标检测（使用白名单）
    /// </summary>
    /// <param name="whitelist">进程白名单，仅当这些进程在前台时检测</param>
    /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
    /// <param name="enableDebugLog">是否启用调试日志，默认 false</param>
    void StartWithWhitelist(HashSet<string> whitelist, int intervalMs = 200, bool enableDebugLog = false);

    /// <summary>
    /// 停止鼠标检测
    /// </summary>
    void Stop();

    /// <summary>
    /// 暂停鼠标检测（全屏时使用）
    /// 暂停期间不会触发事件，恢复时会重新检测当前状态
    /// </summary>
    void Suspend();

    /// <summary>
    /// 恢复鼠标检测（退出全屏时使用）
    /// 恢复后会立即检测当前状态并触发相应事件
    /// </summary>
    void Resume();

    /// <summary>
    /// 是否处于暂停状态
    /// </summary>
    bool IsSuspended { get; }
}
}
