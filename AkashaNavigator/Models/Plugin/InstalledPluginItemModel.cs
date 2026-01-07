using System.Windows;

namespace AkashaNavigator.Models.Plugin
{
    /// <summary>
    /// 已安装插件项数据模型
    /// 用于 InstalledPluginsPage 的 ItemsControl 数据绑定
    /// </summary>
    public class InstalledPluginItemModel
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本号
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 插件作者
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// 被引用的 Profile 数量
        /// </summary>
        public int ReferenceCount { get; set; }

        /// <summary>
        /// 关联的 Profile 文本
        /// </summary>
        public string ProfilesText { get; set; } = "无";

        /// <summary>
        /// 是否有描述（用于描述可见性绑定）
        /// </summary>
        public bool HasDescription { get; set; }

        /// <summary>
        /// 描述可见性
        /// </summary>
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        // 更新相关属性

        /// <summary>
        /// 是否有可用更新
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// 可用的新版本号
        /// </summary>
        public string? AvailableVersion { get; set; }

        /// <summary>
        /// 更新按钮可见性
        /// </summary>
        public Visibility UpdateButtonVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 更新按钮文本
        /// </summary>
        public string UpdateButtonText => $"更新到 v{AvailableVersion}";

        /// <summary>
        /// 更新可用标签可见性
        /// </summary>
        public Visibility UpdateAvailableTagVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 更新可用标签文本
        /// </summary>
        public string UpdateAvailableTagText => $"更新可用 v{AvailableVersion}";
    }
}
