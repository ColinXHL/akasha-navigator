using System.Windows;

namespace AkashaNavigator.Models.Plugin
{
    /// <summary>
    /// 可用插件项数据模型
    /// 用于 AvailablePluginsPage 的 ItemsControl 数据绑定
    /// </summary>
    public class AvailablePluginItemModel
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
        /// 源代码目录（用于安装）
        /// </summary>
        public string SourceDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 是否有描述
        /// </summary>
        public bool HasDescription { get; set; }

        /// <summary>
        /// 是否有作者
        /// </summary>
        public bool HasAuthor { get; set; }

        /// <summary>
        /// 描述可见性
        /// </summary>
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 作者可见性
        /// </summary>
        public Visibility HasAuthorVisibility => HasAuthor ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 插件是否已安装
        /// </summary>
        public bool IsInstalled { get; set; }
    }
}
