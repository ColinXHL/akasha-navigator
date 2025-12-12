namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 应用配置模型
    /// 用于 JSON 序列化存储用户设置（Phase 15）
    /// </summary>
    public class AppConfig
    {
        #region Video Control

        /// <summary>
        /// 快进/倒退秒数
        /// </summary>
        public int SeekSeconds { get; set; } = AppConstants.DefaultSeekSeconds;

        #endregion

        #region Opacity

        /// <summary>
        /// 默认透明度
        /// </summary>
        public double DefaultOpacity { get; set; } = AppConstants.MaxOpacity;

        #endregion

        #region Hotkeys

        /// <summary>
        /// 快进键（虚拟键码）
        /// </summary>
        public uint HotkeySeekForward { get; set; } = Helpers.Win32Helper.VK_6;

        /// <summary>
        /// 倒退键（虚拟键码）
        /// </summary>
        public uint HotkeySeekBackward { get; set; } = Helpers.Win32Helper.VK_5;

        /// <summary>
        /// 播放/暂停键（虚拟键码）
        /// </summary>
        public uint HotkeyTogglePlay { get; set; } = Helpers.Win32Helper.VK_OEM_3;

        /// <summary>
        /// 增加透明度键（虚拟键码）
        /// </summary>
        public uint HotkeyIncreaseOpacity { get; set; } = Helpers.Win32Helper.VK_8;

        /// <summary>
        /// 降低透明度键（虚拟键码）
        /// </summary>
        public uint HotkeyDecreaseOpacity { get; set; } = Helpers.Win32Helper.VK_7;

        /// <summary>
        /// 切换鼠标穿透键（虚拟键码）
        /// </summary>
        public uint HotkeyToggleClickThrough { get; set; } = Helpers.Win32Helper.VK_0;

        #endregion

        #region Window Behavior

        /// <summary>
        /// 是否启用边缘吸附
        /// </summary>
        public bool EnableEdgeSnap { get; set; } = true;

        /// <summary>
        /// 边缘吸附阈值（像素）
        /// </summary>
        public int SnapThreshold { get; set; } = AppConstants.SnapThreshold;

        #endregion
    }
}
