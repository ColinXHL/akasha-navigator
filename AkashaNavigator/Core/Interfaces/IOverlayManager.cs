using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core.Interfaces
{
    /// <summary>
    /// 覆盖层窗口管理服务接口
    /// 管理插件创建的覆盖层窗口实例
    /// </summary>
    public interface IOverlayManager
    {
        /// <summary>
        /// 为插件创建覆盖层窗口
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="options">覆盖层选项（可选）</param>
        /// <returns>创建的覆盖层窗口</returns>
        OverlayWindow CreateOverlay(string pluginId, OverlayOptions? options = null);

        /// <summary>
        /// 获取插件的覆盖层窗口
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>覆盖层窗口，不存在则返回 null</returns>
        OverlayWindow? GetOverlay(string pluginId);

        /// <summary>
        /// 销毁插件的覆盖层窗口
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        void DestroyOverlay(string pluginId);

        /// <summary>
        /// 销毁所有覆盖层窗口
        /// </summary>
        void DestroyAllOverlays();

        /// <summary>
        /// 显示方向标记
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="direction">方向</param>
        /// <param name="durationMs">显示时长（毫秒），0 表示常驻</param>
        void ShowDirectionMarker(string pluginId, Direction direction, int durationMs = 0);

        /// <summary>
        /// 清除插件的所有方向标记
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        void ClearMarkers(string pluginId);
    }
}
