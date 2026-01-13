using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Win32;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 我的 Profile 页面 - 显示 Profile 详情和插件清单
/// </summary>
public partial class MyProfilesPage : UserControl
{
    private readonly MyProfilesPageViewModel _viewModel;
    private readonly IDialogFactory _dialogFactory;
    private readonly IProfileManager _profileManager;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IPluginHost _pluginHost;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;

    // DI构造函数
    public MyProfilesPage(MyProfilesPageViewModel viewModel, IDialogFactory dialogFactory,
                          IProfileManager profileManager, IPluginLibrary pluginLibrary, IPluginHost pluginHost,
                          IPluginAssociationManager pluginAssociationManager, INotificationService notificationService,
                          IEventBus eventBus)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        _pluginAssociationManager =
            pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        InitializeComponent();
        DataContext = _viewModel;

        Loaded += MyProfilesPage_Loaded;

        // 订阅 ViewModel 的事件
        _viewModel.NewProfileRequested += OnNewProfileRequested;
        _viewModel.EditProfileRequested += OnEditProfileRequested;
        _viewModel.DeleteProfileRequested += OnDeleteProfileRequested;
        _viewModel.AddPluginRequested += OnAddPluginRequested;
        _viewModel.ExportRequested += OnExportRequested;
        _viewModel.ImportRequested += OnImportRequested;
        _viewModel.OpenPluginSettingsRequested += OnOpenPluginSettingsRequested;
    }

    private void MyProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshProfileList();
    }

    /// <summary>
    /// 刷新 Profile 列表（公共方法供外部调用）
    /// </summary>
    public void RefreshProfileList()
    {
        _viewModel.RefreshProfileList();
    }

    /// <summary>
    /// 插件启用/禁用切换（UI 事件处理，委托给 ViewModel）
    /// </summary>
    private void PluginToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string pluginId)
        {
            var enabled = checkBox.IsChecked ?? false;
            _viewModel.TogglePluginEnabled(pluginId, enabled);
        }
    }

    /// <summary>
    /// 新建 Profile 请求事件处理（显示对话框）
    /// </summary>
    private void OnNewProfileRequested(object? sender, EventArgs e)
    {
        var dialog = _dialogFactory.CreateProfileCreateDialog();
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && dialog.IsConfirmed && !string.IsNullOrEmpty(dialog.ProfileId))
        {
            // 刷新 Profile 列表
            _viewModel.RefreshProfileList();

            // 选中新创建的 Profile
            SelectProfile(dialog.ProfileId);
        }
    }

    /// <summary>
    /// 编辑 Profile 请求事件处理（显示对话框）
    /// </summary>
    private void OnEditProfileRequested(object? sender, GameProfile profile)
    {
        var dialog = _dialogFactory.CreateProfileEditDialog(profile);
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && dialog.IsConfirmed)
        {
            // 刷新 Profile 列表以显示更新后的名称
            _viewModel.RefreshProfileList();
        }
    }

    /// <summary>
    /// 删除 Profile 请求事件处理（显示确认对话框）
    /// </summary>
    private async void OnDeleteProfileRequested(object? sender, string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
            return;

        var profile = _profileManager.GetProfileById(profileId);
        var profileName = profile?.Name ?? profileId;

        // 获取仅此 Profile 使用的插件列表（引用计数为 1）
        var profilePlugins = _pluginAssociationManager.GetPluginsInProfile(profileId);
        var uniquePluginIds =
            profilePlugins.Where(p => _pluginAssociationManager.GetPluginReferenceCount(p.PluginId) == 1)
                .Select(p => p.PluginId)
                .ToList();

        List<string>? pluginsToUninstall = null;

        if (uniquePluginIds.Count > 0)
        {
            // 有唯一插件，显示 PluginUninstallDialog
            var pluginItems = uniquePluginIds
                                  .Select(pluginId =>
                                          {
                                              var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
                                              return new PluginUninstallItem {
                                                  PluginId = pluginId, Name = pluginInfo?.Name ?? pluginId,
                                                  Description = pluginInfo?.Description ?? string.Empty,
                                                  IsSelected = true // 默认选中
                                              };
                                          })
                                  .ToList();

            var dialog = _dialogFactory.CreatePluginUninstallDialog(profileName, pluginItems);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() != true || !dialog.Confirmed)
            {
                // 用户取消
                return;
            }

            pluginsToUninstall = dialog.SelectedPluginIds;
        }
        else
        {
            // 没有唯一插件，使用 NotificationService 显示确认对话框
            var confirmed = await _notificationService.ConfirmAsync(
                $"确定要删除 Profile \"{profileName}\" 吗？\n\n此操作将删除该 Profile 的配置文件。", "确认删除");

            if (!confirmed)
            {
                return;
            }
        }

        // 执行删除
        await DeleteProfileAsync(profileId, profileName, pluginsToUninstall);
    }

    /// <summary>
    /// 执行 Profile 删除
    /// </summary>
    private System.Threading.Tasks.Task DeleteProfileAsync(string profileId, string profileName,
                                                           List<string>? pluginsToUninstall)
    {
        // 执行删除
        var deleteResult = _profileManager.DeleteProfile(profileId);

        if (deleteResult.IsSuccess)
        {
            // 如果用户选择了要卸载的插件，执行卸载
            if (pluginsToUninstall != null && pluginsToUninstall.Count > 0)
            {
                int successCount = 0;
                int failCount = 0;

                foreach (var pluginId in pluginsToUninstall)
                {
                    var result = _pluginLibrary.UninstallPlugin(pluginId);
                    if (result.IsSuccess)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }

                // 通知其他页面刷新插件列表
                _eventBus.Publish(new PluginListChangedEvent());

                if (failCount > 0)
                {
                    _notificationService.Warning(
                        $"Profile \"{profileName}\" 已删除。插件卸载: 成功 {successCount} 个，失败 {failCount} 个");
                }
                else
                {
                    _notificationService.Success($"Profile \"{profileName}\" 已删除，同时卸载了 {successCount} 个插件");
                }
            }
            else
            {
                _notificationService.Success($"Profile \"{profileName}\" 已删除");
            }

            // 通知 Profile 市场页面刷新（更新安装状态）
            _eventBus.Publish(new ProfileListChangedEvent());

            // 刷新 Profile 列表（会自动切换到默认 Profile）
            _viewModel.RefreshProfileList();
        }
        else
        {
            _notificationService.Error($"删除失败: {deleteResult.Error?.Message}");
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// 添加插件请求事件处理（显示对话框）
    /// </summary>
    private void OnAddPluginRequested(object? sender, string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
            return;

        // 获取已安装但未添加到当前 Profile 的插件
        var installedPlugins = _pluginLibrary.GetInstalledPlugins();
        var currentPlugins = _pluginAssociationManager.GetPluginsInProfile(profileId)
                                 .Select(r => r.PluginId)
                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availablePlugins = installedPlugins.Where(p => !currentPlugins.Contains(p.Id)).ToList();

        if (availablePlugins.Count == 0)
        {
            // 使用 INotificationService 显示消息
            _notificationService.Info("没有可添加的插件，请先在「已安装插件」页面安装插件");
            return;
        }

        var dialog = _dialogFactory.CreatePluginSelectorDialog(availablePlugins, profileId);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshPluginList();
        }
    }

    /// <summary>
    /// 导出请求事件处理（显示文件保存对话框）
    /// </summary>
    private void OnExportRequested(object? sender, GameProfile profile)
    {
        var dialog = new SaveFileDialog { Title = "导出 Profile", Filter = "JSON 文件 (*.json)|*.json",
                                          FileName = $"{profile.Name ?? _viewModel.CurrentProfileId}_profile.json",
                                          DefaultExt = ".json" };

        if (dialog.ShowDialog() == true)
        {
            var profileId = _viewModel.CurrentProfileId ?? "";
            var success = _profileManager.ExportProfileToFile(profileId, dialog.FileName);
            if (success)
            {
                _notificationService.Success($"Profile 已导出到: {dialog.FileName}");
            }
            else
            {
                _notificationService.Error("导出失败，请查看日志获取详细信息");
            }
        }
    }

    /// <summary>
    /// 导入请求事件处理（显示文件打开对话框）
    /// </summary>
    private async void OnImportRequested(object? sender, EventArgs e)
    {
        var dialog =
            new OpenFileDialog { Title = "导入 Profile", Filter = "JSON 文件 (*.json)|*.json", DefaultExt = ".json" };

        if (dialog.ShowDialog() != true)
            return;

        await HandleImportProfile(dialog.FileName);
    }

    /// <summary>
    /// 处理 Profile 导入
    /// </summary>
    private async System.Threading.Tasks.Task HandleImportProfile(string fileName)
    {
        var data = ProfileExportData.LoadFromFile(fileName);
        if (data == null)
        {
            _notificationService.Error("无法解析导入文件，请确保文件格式正确");
            return;
        }

        // 预览导入
        var preview = _profileManager.PreviewImport(data);

        // 构建确认消息
        var message = $"即将导入 Profile: {data.ProfileName}\n包含 {data.PluginReferences.Count} 个插件引用";

        if (preview.MissingPlugins.Count > 0)
        {
            message += $"\n\n⚠ {preview.MissingPlugins.Count} 个插件缺失，导入后可一键安装";
        }

        bool overwrite = false;
        if (preview.ProfileExists)
        {
            var overwriteConfirmed = await _notificationService.ConfirmAsync(
                $"Profile \"{data.ProfileId}\" 已存在。\n\n是否覆盖现有 Profile？", "Profile 已存在");

            if (!overwriteConfirmed)
            {
                _notificationService.Info("导入已取消");
                return;
            }
            overwrite = true;
        }
        else
        {
            var confirmed = await _notificationService.ConfirmAsync(message, "确认导入");
            if (!confirmed)
                return;
        }

        // 执行导入
        var importResult = _profileManager.ImportProfile(data, overwrite);

        if (importResult.IsSuccess)
        {
            _viewModel.RefreshProfileList();

            // 选中导入的 Profile
            SelectProfile(data.ProfileId);

            var successMessage = $"Profile \"{data.ProfileName}\" 导入成功！";
            if (importResult.MissingPlugins.Count > 0)
            {
                successMessage += $" ({importResult.MissingPlugins.Count} 个插件缺失，可一键安装)";
            }

            _notificationService.Success(successMessage);
        }
        else
        {
            _notificationService.Error($"导入失败: {importResult.ErrorMessage}");
        }
    }

    /// <summary>
    /// 打开插件设置请求事件处理（显示对话框）
    /// </summary>
    private void OnOpenPluginSettingsRequested(object? sender, string? pluginId)
    {
        // profileId 从 ViewModel 获取
        var profileId = _viewModel.CurrentProfileId ?? "";
        ShowPluginSettingsDialog(pluginId ?? "", profileId);
    }

    /// <summary>
    /// 显示插件设置对话框
    /// </summary>
    private void ShowPluginSettingsDialog(string pluginId, string profileId)
    {
        var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
        if (pluginInfo == null)
        {
            _notificationService.Error($"找不到插件 {pluginId}");
            return;
        }

        var pluginDirectory = _pluginLibrary.GetPluginDirectory(pluginId);
        var configDirectory = _pluginHost.GetPluginConfigDirectory(profileId, pluginId);

        PluginSettingsWindow.ShowSettings(pluginId, pluginInfo.Name, pluginDirectory, configDirectory,
                                          Window.GetWindow(this), profileId);
    }

    /// <summary>
    /// 选中指定的 Profile
    /// </summary>
    private void SelectProfile(string profileId)
    {
        foreach (var vm in _viewModel.Profiles)
        {
            if (vm.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.SelectedProfile = vm;
                break;
            }
        }
    }
}
}
