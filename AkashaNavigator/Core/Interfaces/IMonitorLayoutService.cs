using AkashaNavigator.Helpers;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// 显示器布局服务接口
/// 负责枚举显示器、解析窗口所在显示器、提供工作区/完整区域，
/// 并在显示拓扑变化时发布事件
/// </summary>
public interface IMonitorLayoutService
{
    /// <summary>
    /// 获取包含指定窗口的显示器信息
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>显示器快照信息，失败返回 null</returns>
    MonitorInfo? GetMonitorFromWindow(IntPtr hwnd);

    /// <summary>
    /// 获取当前的显示器信息（包含窗口所在显示器的完整区域和工作区域）
    /// 使用 MONITOR_DEFAULTTONEAREST 策略
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>显示器快照信息</returns>
    MonitorInfo GetMonitorFromWindowOrDefault(IntPtr hwnd);

    /// <summary>
    /// 根据 DeviceName 查找显示器
    /// </summary>
    /// <param name="deviceName">显示器设备名称（如 "\\.\DISPLAY1"）</param>
    /// <returns>显示器快照信息，找不到返回 null</returns>
    MonitorInfo? FindMonitorByDeviceName(string deviceName);

    /// <summary>
    /// 获取主显示器信息
    /// </summary>
    /// <returns>主显示器快照信息</returns>
    MonitorInfo GetPrimaryMonitor();

    /// <summary>
    /// 刷新显示器拓扑缓存
    /// 在收到系统显示设置变化通知时调用
    /// </summary>
    void Refresh();
}
}