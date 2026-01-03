using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 市场 Profile 详情对话框 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class MarketplaceProfileDetailDialogViewModel : ObservableObject
    {
        private readonly IPluginLibrary _pluginLibrary;

        /// <summary>
        /// Profile 名称
        /// </summary>
        [ObservableProperty]
        private string _profileName = string.Empty;

        /// <summary>
        /// Profile 版本（格式化后）
        /// </summary>
        [ObservableProperty]
        private string _profileVersion = string.Empty;

        /// <summary>
        /// Profile 描述
        /// </summary>
        [ObservableProperty]
        private string _profileDescription = string.Empty;

        /// <summary>
        /// 目标游戏名称
        /// </summary>
        [ObservableProperty]
        private string _targetGame = string.Empty;

        /// <summary>
        /// 是否显示目标游戏标签
        /// </summary>
        [ObservableProperty]
        private bool _showTargetGame;

        /// <summary>
        /// 作者名称
        /// </summary>
        [ObservableProperty]
        private string _author = string.Empty;

        /// <summary>
        /// 更新时间（格式化后）
        /// </summary>
        [ObservableProperty]
        private string _updatedAt = string.Empty;

        /// <summary>
        /// 插件数量（格式化后）
        /// </summary>
        [ObservableProperty]
        private string _pluginCount = string.Empty;

        /// <summary>
        /// 对话框结果：true=安装，false=取消
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<PluginStatusItem> PluginList { get; } = new();

        /// <summary>
        /// 对话框请求关闭事件
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MarketplaceProfileDetailDialogViewModel(IPluginLibrary pluginLibrary)
        {
            _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        }

        /// <summary>
        /// 初始化 ViewModel（设置 Profile 数据）
        /// </summary>
        public void Initialize(MarketplaceProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            // 基本信息
            ProfileName = profile.Name;
            ProfileVersion = $"v{profile.Version}";
            ProfileDescription = string.IsNullOrWhiteSpace(profile.Description)
                ? "暂无描述"
                : profile.Description;

            // 目标游戏
            TargetGame = profile.TargetGame;
            ShowTargetGame = !string.IsNullOrWhiteSpace(profile.TargetGame);

            // 元信息
            Author = string.IsNullOrWhiteSpace(profile.Author) ? "未知" : profile.Author;
            UpdatedAt = profile.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
            PluginCount = $"{profile.PluginCount} 个";

            // 插件列表
            LoadPluginList(profile.PluginIds);
        }

        /// <summary>
        /// 加载插件列表
        /// </summary>
        private void LoadPluginList(System.Collections.Generic.List<string> pluginIds)
        {
            PluginList.Clear();
            foreach (var pluginId in pluginIds)
            {
                var isInstalled = _pluginLibrary.IsInstalled(pluginId);
                PluginList.Add(new PluginStatusItem(pluginId, isInstalled));
            }
        }

        /// <summary>
        /// 安装命令（自动生成 InstallCommand）
        /// </summary>
        [RelayCommand]
        private void Install()
        {
            DialogResult = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 关闭命令（返回 false）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 插件状态项模型
    /// </summary>
    public class PluginStatusItem
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// 是否已安装
        /// </summary>
        public bool IsInstalled { get; }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText => IsInstalled ? "已安装" : "缺失";

        /// <summary>
        /// 状态前景色
        /// </summary>
        public Brush StatusForeground => IsInstalled
            ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))  // 绿色
            : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)); // 红色

        /// <summary>
        /// 状态背景色
        /// </summary>
        public Brush StatusBackground => IsInstalled
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A))  // 深绿背景
            : new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A)); // 深红背景

        /// <summary>
        /// 构造函数
        /// </summary>
        public PluginStatusItem(string pluginId, bool isInstalled)
        {
            PluginId = pluginId;
            IsInstalled = isInstalled;
        }
    }
}
