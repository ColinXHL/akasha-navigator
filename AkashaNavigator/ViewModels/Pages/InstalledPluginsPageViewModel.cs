using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.ViewModels.Pages
{
    /// <summary>
    /// 已安装插件页面的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class InstalledPluginsPageViewModel : ObservableObject
    {
        private readonly IPluginLibrary _pluginLibrary;
        private readonly IPluginAssociationManager _pluginAssociationManager;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<InstalledPluginItemModel> Plugins { get; } = new();

        /// <summary>
        /// 搜索文本（自动生成 SearchText 属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// 插件数量文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _pluginCountText = "共 0 个插件";

        /// <summary>
        /// 是否无插件（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _isEmpty;

        /// <summary>
        /// 检查更新结果缓存
        /// </summary>
        private Dictionary<string, UpdateCheckResult> _updateCache = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        public InstalledPluginsPageViewModel(
            IPluginLibrary pluginLibrary,
            IPluginAssociationManager pluginAssociationManager,
            INotificationService notificationService)
        {
            _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
            _pluginAssociationManager = pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// 页面加载时检查更新并刷新插件列表
        /// </summary>
        [RelayCommand]
        public void OnLoaded()
        {
            CheckAndRefreshPluginList();
        }

        /// <summary>
        /// 搜索文本变化时重新加载（自动生成的方法）
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 检查更新并刷新插件列表
        /// </summary>
        public void CheckAndRefreshPluginList()
        {
            var updates = _pluginLibrary.CheckAllUpdates();
            _updateCache = updates.ToDictionary(u => u.PluginId, u => u) ?? new Dictionary<string, UpdateCheckResult>();
            RefreshPluginList();
        }

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        public void RefreshPluginList()
        {
            var plugins = _pluginLibrary.GetInstalledPlugins();
            var searchText = SearchText?.ToLower() ?? "";

            // 过滤搜索
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                plugins = plugins
                    .Where(p => p.Name.ToLower().Contains(searchText) ||
                                (p.Description?.ToLower().Contains(searchText) ?? false))
                    .ToList();
            }

            // 转换为视图模型
            var viewModels = plugins
                .Select(p => CreatePluginItemModel(p))
                .ToList();

            Plugins.Clear();
            foreach (var vm in viewModels)
            {
                Plugins.Add(vm);
            }

            PluginCountText = $"共 {viewModels.Count} 个插件";
            IsEmpty = viewModels.Count == 0;
        }

        /// <summary>
        /// 创建插件项目模型
        /// </summary>
        private InstalledPluginItemModel CreatePluginItemModel(InstalledPluginInfo plugin)
        {
            var model = new InstalledPluginItemModel
            {
                Id = plugin.Id,
                Name = plugin.Name,
                Version = plugin.Version,
                Description = plugin.Description,
                Author = plugin.Author,
                ReferenceCount = _pluginAssociationManager.GetPluginReferenceCount(plugin.Id),
                ProfilesText = GetProfilesText(plugin.Id),
                HasDescription = !string.IsNullOrWhiteSpace(plugin.Description)
            };

            // 设置更新信息
            if (_updateCache.TryGetValue(plugin.Id, out var updateInfo) && updateInfo.HasUpdate)
            {
                model.HasUpdate = true;
                model.AvailableVersion = updateInfo.AvailableVersion;
            }

            return model;
        }

        /// <summary>
        /// 获取插件关联的 Profile 文本
        /// </summary>
        private string GetProfilesText(string pluginId)
        {
            var profiles = _pluginAssociationManager.GetProfilesUsingPlugin(pluginId);
            if (profiles.Count == 0)
                return "无";
            if (profiles.Count <= 3)
                return string.Join(", ", profiles);
            return $"{string.Join(", ", profiles.Take(3))} 等 {profiles.Count} 个";
        }

        /// <summary>
        /// 检查更新命令（自动生成 CheckUpdateCommand）
        /// </summary>
        [RelayCommand]
        private void CheckUpdate()
        {
            var updates = _pluginLibrary.CheckAllUpdates();
            _updateCache = updates.ToDictionary(u => u.PluginId, u => u) ?? new Dictionary<string, UpdateCheckResult>();

            if (updates.Count == 0)
            {
                _notificationService.Show("所有插件都是最新版本", NotificationType.Success);
            }
            else
            {
                RefreshPluginList();
                _notificationService.Show($"发现 {updates.Count} 个插件有可用更新", NotificationType.Info);
            }
        }

        /// <summary>
        /// 更新插件命令（自动生成 UpdatePluginCommand）
        /// </summary>
        [RelayCommand]
        private void UpdatePlugin(string? pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;

            var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
            var pluginName = pluginInfo?.Name ?? pluginId;

            var result = _pluginLibrary.UpdatePlugin(pluginId);

            if (result.IsSuccess)
            {
                _notificationService.Show($"{pluginName} 已更新到 v{result.NewVersion}", NotificationType.Success);
                RefreshPluginList();
            }
            else
            {
                _notificationService.Show($"更新 {pluginName} 失败: {result.ErrorMessage}", NotificationType.Error);
            }
        }

        /// <summary>
        /// 添加到 Profile 命令（自动生成 AddToProfileCommand）
        /// 注意：此命令需要由 Code-behind 处理对话框显示
        /// </summary>
        [RelayCommand]
        private void AddToProfile(string? pluginId)
        {
            // 此命令由 Code-behind 的 AddToProfileRequested 事件处理
            AddToProfileRequested?.Invoke(this, pluginId);
        }

        /// <summary>
        /// 卸载插件命令（自动生成 UninstallCommand）
        /// 注意：此命令需要由 Code-behind 处理对话框显示
        /// </summary>
        [RelayCommand]
        private void Uninstall(string? pluginId)
        {
            // 此命令由 Code-behind 的 UninstallRequested 事件处理
            UninstallRequested?.Invoke(this, pluginId);
        }

        /// <summary>
        /// 添加到 Profile 请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<string?>? AddToProfileRequested;

        /// <summary>
        /// 卸载请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<string?>? UninstallRequested;
    }
}
