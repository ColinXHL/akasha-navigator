using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 可用插件页面 - 显示所有内置插件（包括已安装和未安装）
/// </summary>
public partial class AvailablePluginsPage : UserControl
{
    private readonly PluginLibrary _pluginLibrary;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public AvailablePluginsPage(PluginLibrary pluginLibrary, INotificationService notificationService)
    {
        _pluginLibrary = pluginLibrary;
        _notificationService = notificationService;
        InitializeComponent();
        Loaded += AvailablePluginsPage_Loaded;
    }

    private void AvailablePluginsPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 刷新插件列表
    /// </summary>
    public void RefreshPluginList()
    {
        var allPlugins = GetAllBuiltinPlugins();
        var searchText = SearchBox?.Text?.ToLower() ?? "";

        // 过滤搜索
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            allPlugins = allPlugins
                             .Where(p => p.Name.ToLower().Contains(searchText) ||
                                         (p.Description?.ToLower().Contains(searchText) ?? false))
                             .ToList();
        }

        PluginList.ItemsSource = allPlugins;
        PluginCountText.Text = $"共 {allPlugins.Count} 个插件";
        NoPluginsText.Visibility = allPlugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 获取所有内置插件列表（包括已安装和未安装）
    /// </summary>
    private List<AvailablePluginViewModel> GetAllBuiltinPlugins()
    {
        var result = new List<AvailablePluginViewModel>();
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
            if (manifest.IsFailure || string.IsNullOrEmpty(manifest.Value.Id))
                continue;

            var isInstalled = installedIds.Contains(manifest.Value.Id);

            result.Add(new AvailablePluginViewModel {
                Id = manifest.Value.Id, Name = manifest.Value.Name ?? manifest.Value.Id, Version = manifest.Value.Version ?? "1.0.0",
                Description = manifest.Value.Description, Author = manifest.Value.Author, SourceDirectory = pluginDir,
                HasDescription = !string.IsNullOrWhiteSpace(manifest.Value.Description),
                HasAuthor = !string.IsNullOrWhiteSpace(manifest.Value.Author), IsInstalled = isInstalled
            });
        }

        return result;
    }

    /// <summary>
    /// 搜索框文本变化
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 安装按钮点击
    /// </summary>
    private void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AvailablePluginViewModel viewModel)
        {
            var result = _pluginLibrary.InstallPlugin(viewModel.Id, viewModel.SourceDirectory);
            if (result.IsSuccess)
            {
                _notificationService.Success($"插件 \"{viewModel.Name}\" 安装成功！");

                // 更新视图模型状态
                viewModel.IsInstalled = true;

                // 通知父窗口刷新
                if (Window.GetWindow(this) is PluginCenterWindow centerWindow)
                {
                    centerWindow.RefreshCurrentPage();
                }
            }
            else
            {
                _notificationService.Error($"安装失败: {result.ErrorMessage}");
            }
        }
    }

    /// <summary>
    /// 卸载按钮点击
    /// </summary>
    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AvailablePluginViewModel viewModel)
        {
            // 显示卸载确认对话框（包含引用信息）
            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateUninstallConfirmDialog(viewModel.Id, viewModel.Name);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.UninstallSucceeded)
            {
                // 更新视图模型状态
                viewModel.IsInstalled = false;

                // 通知父窗口刷新
                if (Window.GetWindow(this) is PluginCenterWindow centerWindow)
                {
                    centerWindow.RefreshCurrentPage();
                }
            }
        }
    }
}

/// <summary>
/// 可用插件视图模型
/// </summary>
public class AvailablePluginViewModel : INotifyPropertyChanged
{
    private bool _isInstalled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string SourceDirectory { get; set; } = string.Empty;
    public bool HasDescription { get; set; }
    public bool HasAuthor { get; set; }
    public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasAuthorVisibility => HasAuthor ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 插件是否已安装
    /// </summary>
    public bool IsInstalled
    {
        get => _isInstalled;
        set {
            if (_isInstalled != value)
            {
                _isInstalled = value;
                OnPropertyChanged(nameof(IsInstalled));
            }
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
}
