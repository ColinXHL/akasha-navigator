using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.ViewModels.Pages
{
    /// <summary>
    /// 可用插件页面的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class AvailablePluginsPageViewModel : ObservableObject
    {
        private readonly IPluginLibrary _pluginLibrary;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<AvailablePluginItemModel> Plugins { get; } = new();

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
        /// 构造函数
        /// </summary>
        public AvailablePluginsPageViewModel(
            IPluginLibrary pluginLibrary,
            INotificationService notificationService)
        {
            _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// 页面加载时刷新插件列表
        /// </summary>
        [RelayCommand]
        public void OnLoaded()
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 搜索文本变化时重新加载（自动生成的方法）
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        public void RefreshPluginList()
        {
            var allPlugins = GetAllBuiltinPlugins();
            var searchText = SearchText?.ToLower() ?? "";

            // 过滤搜索
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                allPlugins = allPlugins
                    .Where(p => p.Name.ToLower().Contains(searchText) ||
                                (p.Description?.ToLower().Contains(searchText) ?? false))
                    .ToList();
            }

            Plugins.Clear();
            foreach (var plugin in allPlugins)
            {
                Plugins.Add(plugin);
            }

            PluginCountText = $"共 {allPlugins.Count} 个插件";
            IsEmpty = allPlugins.Count == 0;
        }

        /// <summary>
        /// 获取所有内置插件列表（包括已安装和未安装）
        /// </summary>
        private List<AvailablePluginItemModel> GetAllBuiltinPlugins()
        {
            var result = new List<AvailablePluginItemModel>();
            var installedIds = _pluginLibrary.GetInstalledPlugins().Select(p => p.Id).ToHashSet();

            // 扫描内置插件目录
            var builtinPluginsDir = AppPaths.BuiltInPluginsDirectory;
            if (!Directory.Exists(builtinPluginsDir))
                return result;

            foreach (var pluginDir in Directory.GetDirectories(builtinPluginsDir))
            {
                var manifestPath = Path.Combine(pluginDir, "plugin.json");
                if (!File.Exists(manifestPath))
                    continue;

                var manifest = JsonHelper.LoadFromFile<PluginManifest>(manifestPath);
                if (manifest.IsFailure || string.IsNullOrEmpty(manifest.Value!.Id))
                    continue;

                var isInstalled = installedIds.Contains(manifest.Value.Id);

                result.Add(new AvailablePluginItemModel
                {
                    Id = manifest.Value.Id,
                    Name = manifest.Value.Name ?? manifest.Value.Id,
                    Version = manifest.Value.Version ?? "1.0.0",
                    Description = manifest.Value.Description,
                    Author = manifest.Value.Author,
                    SourceDirectory = pluginDir,
                    HasDescription = !string.IsNullOrWhiteSpace(manifest.Value.Description),
                    HasAuthor = !string.IsNullOrWhiteSpace(manifest.Value.Author),
                    IsInstalled = isInstalled
                });
            }

            return result;
        }

        /// <summary>
        /// 安装插件命令（自动生成 InstallCommand）
        /// </summary>
        [RelayCommand]
        private void Install(AvailablePluginItemModel? plugin)
        {
            if (plugin == null)
                return;

            var result = _pluginLibrary.InstallPlugin(plugin.Id, plugin.SourceDirectory);
            if (result.IsSuccess)
            {
                _notificationService.Show($"插件 \"{plugin.Name}\" 安装成功！", NotificationType.Success);

                // 更新插件状态
                plugin.IsInstalled = true;

                // 通知刷新
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _notificationService.Show($"安装失败: {result.ErrorMessage}", NotificationType.Error);
            }
        }

        /// <summary>
        /// 卸载请求命令（自动生成 UninstallCommand）
        /// 注意：此命令需要由 Code-behind 处理对话框显示
        /// </summary>
        [RelayCommand]
        private void Uninstall(AvailablePluginItemModel? plugin)
        {
            if (plugin == null)
                return;

            // 此命令由 Code-behind 的 UninstallRequested 事件处理
            UninstallRequested?.Invoke(this, plugin);
        }

        /// <summary>
        /// 刷新请求事件（由 Code-behind 订阅）
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// 卸载请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<AvailablePluginItemModel>? UninstallRequested;
    }
}
