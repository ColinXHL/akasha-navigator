using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// OSD（On-Screen Display）管理器
    /// 负责显示屏幕提示信息
    /// </summary>
    public class OsdManager
    {
        private OsdWindow? _osdWindow;

        /// <summary>
        /// 显示 OSD 提示
        /// </summary>
        /// <param name="message">提示文字</param>
        /// <param name="icon">图标（可选）</param>
        public void ShowMessage(string message, string? icon = null)
        {
            // 延迟初始化 OSD 窗口
            _osdWindow ??= new OsdWindow();
            _osdWindow.ShowMessage(message, icon);
        }
    }
}
