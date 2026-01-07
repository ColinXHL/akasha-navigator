using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Models.Profile
{
    /// <summary>
    /// Profile 选择器数据模型
    /// </summary>
    public class ProfileSelectorModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsCurrent { get; set; } = false;
    }

    /// <summary>
    /// Profile 插件数据模型
    /// </summary>
    public class ProfilePluginModel
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;
        public PluginInstallStatus Status { get; set; } = PluginInstallStatus.Installed;

        /// <summary>
        /// 是否是从原始列表中移除的插件（用于市场 Profile）
        /// </summary>
        public bool IsRemovedFromOriginal { get; set; } = false;

        /// <summary>
        /// 是否可以切换启用状态（缺失的插件不能切换）
        /// </summary>
        public bool CanToggle => Status != PluginInstallStatus.Missing;

        /// <summary>
        /// 是否有描述
        /// </summary>
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        /// <summary>
        /// 描述可见性
        /// </summary>
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 安装按钮可见性（仅缺失时显示）
        /// </summary>
        public Visibility InstallButtonVisibility =>
            Status == PluginInstallStatus.Missing ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 移除按钮文本
        /// </summary>
        public string RemoveButtonText => "移除";

        /// <summary>
        /// 移除按钮可见性（从原始列表移除的插件不显示移除按钮）
        /// </summary>
        public Visibility RemoveButtonVisibility => IsRemovedFromOriginal ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// 添加按钮可见性（仅从原始列表移除的插件显示）
        /// </summary>
        public Visibility AddBackButtonVisibility => IsRemovedFromOriginal ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 设置按钮可见性（仅已安装且未从原始列表移除时显示）
        /// </summary>
        public Visibility SettingsButtonVisibility =>
            (Status == PluginInstallStatus.Installed || Status == PluginInstallStatus.Disabled) && !IsRemovedFromOriginal
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText =>
            Status switch { PluginInstallStatus.Installed => "已安装", PluginInstallStatus.Missing => "缺失",
                            PluginInstallStatus.Disabled => "已禁用",
                            _ => "未知" };

        /// <summary>
        /// 状态颜色
        /// </summary>
        public Brush StatusColor =>
            Status switch { PluginInstallStatus.Installed => new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)), // 绿色
                            PluginInstallStatus.Missing => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),   // 红色
                            PluginInstallStatus.Disabled => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),  // 灰色
                            _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)) };

        /// <summary>
        /// 状态标签样式
        /// </summary>
        public Style StatusTagStyle
        {
            get {
                var style = new Style(typeof(Border));
                style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(3)));
                style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(6, 2, 6, 2)));

                var bgColor = Status switch { PluginInstallStatus.Installed => Color.FromRgb(0x1A, 0x3A, 0x1A),
                                              PluginInstallStatus.Missing => Color.FromRgb(0x3A, 0x1A, 0x1A),
                                              PluginInstallStatus.Disabled => Color.FromRgb(0x2A, 0x2A, 0x2A),
                                              _ => Color.FromRgb(0x2A, 0x2A, 0x2A) };
                style.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(bgColor)));

                return style;
            }
        }
    }
}
