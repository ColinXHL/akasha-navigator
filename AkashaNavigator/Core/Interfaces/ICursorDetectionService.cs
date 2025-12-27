using System;

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
        /// 启动鼠标检测
        /// </summary>
        /// <param name="targetProcessName">目标进程名（不含扩展名），仅当此进程在前台时检测</param>
        /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
        void Start(string? targetProcessName = null, int intervalMs = 200);

        /// <summary>
        /// 停止鼠标检测
        /// </summary>
        void Stop();
    }
}
