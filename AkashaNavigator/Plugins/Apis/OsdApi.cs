using System;
using AkashaNavigator.Core;

namespace AkashaNavigator.Plugins.Apis
{
    /// <summary>
    /// OSD (On-Screen Display) API
    /// 提供屏幕提示功能
    /// </summary>
    public class OsdApi
    {
        private readonly string _pluginId;
        private readonly OsdManager _osdManager;

        /// <summary>
        /// 创建 OSD API 实例
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="osdManager">OSD 管理器</param>
        public OsdApi(string pluginId, OsdManager osdManager)
        {
            _pluginId = pluginId;
            _osdManager = osdManager ?? throw new ArgumentNullException(nameof(osdManager));
        }

        /// <summary>
        /// 显示 OSD 提示
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="icon">图标（可选）</param>
        public void show(string message, string? icon = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Services.LogService.Instance.Warn($"Plugin:{_pluginId}", "OsdApi.show: message is empty");
                return;
            }

            try
            {
                _osdManager.ShowMessage(message, icon);
                Services.LogService.Instance.Debug($"Plugin:{_pluginId}", "OSD shown: {Message}", message);
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_pluginId}", "OsdApi.show failed: {ErrorMessage}", ex.Message);
            }
        }
    }
}
