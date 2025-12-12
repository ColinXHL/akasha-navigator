namespace FloatWebPlayer
{
    /// <summary>
    /// 应用程序常量定义
    /// 集中管理所有配置常量，便于后续设置窗口引用和用户自定义
    /// </summary>
    public static class AppConstants
    {
        #region Window - PlayerWindow

        /// <summary>
        /// 窗口最小宽度
        /// </summary>
        public const double MinWindowWidth = 200;

        /// <summary>
        /// 窗口最小高度
        /// </summary>
        public const double MinWindowHeight = 150;

        /// <summary>
        /// 拖拽边框厚度（像素）
        /// </summary>
        public const int ResizeBorderThickness = 8;

        /// <summary>
        /// 边缘吸附阈值（像素），预留给 Phase 13
        /// </summary>
        public const int SnapThreshold = 10;

        #endregion

        #region Opacity

        /// <summary>
        /// 最小透明度
        /// </summary>
        public const double MinOpacity = 0.2;

        /// <summary>
        /// 最大透明度
        /// </summary>
        public const double MaxOpacity = 1.0;

        /// <summary>
        /// 透明度步进
        /// </summary>
        public const double OpacityStep = 0.1;

        #endregion

        #region Video Control

        /// <summary>
        /// 默认快进/倒退秒数
        /// </summary>
        public const int DefaultSeekSeconds = 5;

        #endregion

        #region OSD

        /// <summary>
        /// OSD 淡入动画时长（毫秒）
        /// </summary>
        public const int OsdFadeInDuration = 200;

        /// <summary>
        /// OSD 显示停留时长（毫秒）
        /// </summary>
        public const int OsdDisplayDuration = 1000;

        /// <summary>
        /// OSD 淡出动画时长（毫秒）
        /// </summary>
        public const int OsdFadeOutDuration = 300;

        #endregion

        #region ControlBar

        /// <summary>
        /// 控制栏展开高度
        /// </summary>
        public const double ControlBarExpandedHeight = 50;

        /// <summary>
        /// 控制栏触发线高度
        /// </summary>
        public const double ControlBarTriggerLineHeight = 16;

        /// <summary>
        /// 屏幕顶部触发区域比例
        /// </summary>
        public const double ControlBarTriggerAreaRatio = 1.0 / 4.0;

        /// <summary>
        /// 延迟隐藏时间（毫秒）
        /// </summary>
        public const int ControlBarHideDelayMs = 400;

        /// <summary>
        /// 状态切换防抖时间（毫秒）
        /// </summary>
        public const int ControlBarStateStabilityMs = 150;

        #endregion

        #region URLs

        /// <summary>
        /// 默认首页 URL
        /// </summary>
        public const string DefaultHomeUrl = "https://www.bilibili.com";

        #endregion
    }
}
