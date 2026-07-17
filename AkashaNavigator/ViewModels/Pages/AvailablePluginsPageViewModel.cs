using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;

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
    private readonly IEventBus _eventBus;
    private readonly IUpdateManifestService _updateManifestService;
    private readonly IPluginPackageService _pluginPackageService;
    private readonly IConfigService _configService;
    private readonly Dictionary<string, CancellationTokenSource> _downloads =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AvailablePluginItemModel> _downloadItems =
        new(StringComparer.OrdinalIgnoreCase);

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
        INotificationService notificationService,
        IEventBus eventBus,
        IUpdateManifestService updateManifestService,
        IPluginPackageService pluginPackageService,
        IConfigService configService)
    {
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _updateManifestService =
            updateManifestService ?? throw new ArgumentNullException(nameof(updateManifestService));
        _pluginPackageService =
            pluginPackageService ?? throw new ArgumentNullException(nameof(pluginPackageService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        // 订阅插件列表变化事件
        _eventBus.Subscribe<PluginListChangedEvent>(OnPluginListChanged);
    }

    /// <summary>
    /// 插件列表变化事件处理
    /// </summary>
    private void OnPluginListChanged(PluginListChangedEvent e)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 页面加载时刷新插件列表
    /// </summary>
    [RelayCommand]
    public async Task OnLoadedAsync()
    {
        RefreshPluginList();

        var refreshResult = await _updateManifestService.RefreshAsync();
        if (refreshResult.IsSuccess)
        {
            RefreshPluginList();
        }
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
        var allPlugins = GetAllCatalogPlugins();
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
            Plugins.Add(
                _downloadItems.TryGetValue(plugin.Id, out var activeDownload)
                    ? activeDownload
                    : plugin);
        }

        PluginCountText = $"共 {allPlugins.Count} 个插件";
        IsEmpty = allPlugins.Count == 0;
    }

    /// <summary>
    /// 获取所有内置插件列表（包括已安装和未安装）
    /// </summary>
    private List<AvailablePluginItemModel> GetAllCatalogPlugins()
    {
        var catalog = new Dictionary<string, PluginCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        var installed = _pluginLibrary.GetInstalledPlugins()
            .ToDictionary(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase);

        // 扫描内置插件目录
        var builtinPluginsDir = AppPaths.BuiltInPluginsDirectory;
        if (Directory.Exists(builtinPluginsDir))
        {
            foreach (var pluginDir in Directory.GetDirectories(builtinPluginsDir))
            {
                var manifestPath = Path.Combine(pluginDir, "plugin.json");
                if (!File.Exists(manifestPath))
                    continue;

                var manifest = JsonHelper.LoadFromFile<PluginManifest>(manifestPath);
                if (manifest.IsFailure || string.IsNullOrEmpty(manifest.Value!.Id))
                    continue;

                catalog[manifest.Value.Id] = new PluginCatalogEntry {
                    Id = manifest.Value.Id,
                    Name = manifest.Value.Name ?? manifest.Value.Id,
                    Version = manifest.Value.Version ?? "1.0.0",
                    Description = manifest.Value.Description,
                    Author = manifest.Value.Author,
                    LocalSourceDirectory = pluginDir
                };
            }
        }

        foreach (var remote in _pluginPackageService.GetRemoteCatalog())
        {
            catalog[remote.Id] = remote;
        }

        return catalog.Values
            .Select(entry => CreateItem(entry, installed.GetValueOrDefault(entry.Id)))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 安装插件命令（自动生成 InstallCommand）
    /// </summary>
    [RelayCommand]
    private async Task InstallAsync(AvailablePluginItemModel? plugin)
    {
        if (plugin == null)
            return;

        if (plugin.IsRemote)
        {
            await InstallRemoteAsync(plugin);
            return;
        }

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
            _notificationService.Show($"安装失败: {result.Error?.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void CancelDownload(AvailablePluginItemModel? plugin)
    {
        if (plugin != null && _downloads.TryGetValue(plugin.Id, out var cancellation))
        {
            cancellation.Cancel();
        }
    }

    /// <summary>
    /// 从用户选择的 ZIP 插件包安装或更新插件
    /// </summary>
    public void InstallPackage(string archivePath)
    {
        var result = _pluginLibrary.InstallPluginPackage(archivePath);
        if (result.IsSuccess)
        {
            _notificationService.Show(
                $"插件 \"{result.Value!.Name}\" 导入成功，可在“已安装插件”中管理。",
                NotificationType.Success);
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _notificationService.Show($"插件包导入失败: {result.Error?.Message}", NotificationType.Error);
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

    public void Dispose()
    {
        foreach (var cancellation in _downloads.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        _downloads.Clear();
        _downloadItems.Clear();
        _eventBus.Unsubscribe<PluginListChangedEvent>(OnPluginListChanged);
    }

    private AvailablePluginItemModel CreateItem(
        PluginCatalogEntry entry,
        InstalledPluginInfo? installed)
    {
        var description = entry.Description ?? installed?.Description;
        var author = entry.Author ?? installed?.Author;
        return new AvailablePluginItemModel {
            Id = entry.Id,
            Name = entry.Name,
            Version = entry.Version,
            Description = description,
            Author = author,
            SourceDirectory = entry.LocalSourceDirectory ?? string.Empty,
            IsRemote = entry.IsRemote,
            InstalledVersion = installed?.Version ?? string.Empty,
            InstalledVersionText = installed == null ? "未安装" : $"已安装: v{installed.Version}",
            PackageSizeText = entry.Package == null ? string.Empty : FormatBytes(entry.Package.Size),
            HasDescription = !string.IsNullOrWhiteSpace(description),
            HasAuthor = !string.IsNullOrWhiteSpace(author),
            IsInstalled = installed != null,
            HasUpdate = installed != null &&
                        PluginLibrary.CompareVersions(entry.Version, installed.Version) > 0,
            SelectedSourceText = entry.IsRemote
                ? $"下载源：{GetPreferenceDisplayName(_configService.Config.PluginDownloadSourcePreference)}"
                : string.Empty
        };
    }

    private async Task InstallRemoteAsync(AvailablePluginItemModel plugin)
    {
        if (_downloads.ContainsKey(plugin.Id))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _downloads[plugin.Id] = cancellation;
        _downloadItems[plugin.Id] = plugin;
        var wasInstalled = plugin.IsInstalled;
        plugin.IsDownloading = true;
        plugin.DownloadProgress = 0;
        plugin.DownloadStatus = "正在选择下载源…";

        var progress = new Progress<PluginDownloadProgress>(
            value =>
            {
                plugin.SelectedSourceText = $"下载源：{GetSourceDisplayName(value.SourceId)}";
                plugin.DownloadProgress = Math.Clamp(value.Percentage, 0, 100);
                plugin.DownloadStatus =
                    $"{FormatBytes(value.BytesReceived)} / {FormatBytes(value.TotalBytes)}";
            });

        try
        {
            var result = await _pluginPackageService.InstallOrUpdateAsync(
                plugin.Id,
                progress,
                cancellation.Token);
            if (result.IsSuccess)
            {
                plugin.IsInstalled = true;
                plugin.InstalledVersion = result.Value!.Version;
                plugin.InstalledVersionText = $"已安装: v{result.Value.Version}";
                plugin.HasUpdate = false;
                _notificationService.Show(
                    $"插件 \"{plugin.Name}\" {(wasInstalled ? "更新" : "安装")}成功！",
                    NotificationType.Success);
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (result.Error?.Code == PluginErrorCodes.RemoteDownloadCanceled)
            {
                _notificationService.Show("插件下载已取消", NotificationType.Info);
            }
            else
            {
                _notificationService.Show(
                    $"远程插件安装失败: {result.Error?.Message}\n可使用“从 ZIP 安装”手动导入。",
                    NotificationType.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _notificationService.Show("插件下载已取消", NotificationType.Info);
        }
        finally
        {
            plugin.IsDownloading = false;
            plugin.DownloadStatus = string.Empty;
            _downloads.Remove(plugin.Id);
            _downloadItems.Remove(plugin.Id);
            cancellation.Dispose();
            RefreshPluginList();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var mebibytes = bytes / 1024d / 1024d;
        return mebibytes >= 1
            ? $"{mebibytes:0.##} MiB"
            : $"{bytes / 1024d:0.##} KiB";
    }

    private static string GetPreferenceDisplayName(PluginDownloadSourcePreference preference)
    {
        return preference switch {
            PluginDownloadSourcePreference.GitHub => "GitHub",
            PluginDownloadSourcePreference.Cnb => "CNB",
            _ => "自动选择"
        };
    }

    private static string GetSourceDisplayName(string sourceId)
    {
        return string.Equals(sourceId, "github", StringComparison.OrdinalIgnoreCase)
            ? "GitHub"
            : string.Equals(sourceId, "cnb", StringComparison.OrdinalIgnoreCase)
                ? "CNB"
                : sourceId;
    }
}
}
