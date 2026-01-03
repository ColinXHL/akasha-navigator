using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Views.Windows;
using Microsoft.Win32;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// 我的 Profile 页面 - 显示 Profile 详情和插件清单
/// </summary>
public partial class MyProfilesPage : UserControl
{
    private readonly IProfileManager _profileManager;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly PluginLibrary _pluginLibrary; // 使用具体类，因为需要 InstallPlugin 方法
    private readonly IPluginHost _pluginHost;
    private readonly INotificationService _notificationService;
    private readonly IDialogFactory _dialogFactory;
    private string? _currentProfileId;

    // DI构造函数（推荐使用）
    public MyProfilesPage(
        IProfileManager profileManager,
        IPluginAssociationManager pluginAssociationManager,
        PluginLibrary pluginLibrary,
        IPluginHost pluginHost,
        INotificationService notificationService,
        IDialogFactory dialogFactory)
    {
        _profileManager = profileManager;
        _pluginAssociationManager = pluginAssociationManager;
        _pluginLibrary = pluginLibrary;
        _pluginHost = pluginHost;
        _notificationService = notificationService;
        _dialogFactory = dialogFactory;

        InitializeComponent();
        Loaded += MyProfilesPage_Loaded;
    }

    private void MyProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshProfileList();
    }

    /// <summary>
    /// 刷新 Profile 列表
    /// </summary>
    public void RefreshProfileList()
    {
        var profiles = _profileManager.Profiles;
        var currentProfile = _profileManager.CurrentProfile;

        // 创建 ViewModel 列表
        var viewModels = profiles
                             .Select(p => new ProfileSelectorViewModel {
                                 Id = p.Id, Name = p.Name ?? p.Id,
                                 IsCurrent = p.Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase)
                             })
                             .ToList();

        // 设置 ItemsSource
        ProfileSelector.ItemsSource = viewModels;

        // 选中当前 Profile
        var currentVm = viewModels.FirstOrDefault(vm => vm.IsCurrent);
        if (currentVm != null)
        {
            ProfileSelector.SelectedItem = currentVm;
        }
        else if (viewModels.Count > 0)
        {
            ProfileSelector.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// 刷新插件清单
    /// </summary>
    public void RefreshPluginList()
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            PluginList.ItemsSource = null;
            NoPluginsText.Visibility = Visibility.Visible;
            MissingWarning.Visibility = Visibility.Collapsed;
            PluginCountText.Text = "(0 个插件)";
            return;
        }

        // 获取 Profile 的插件引用
        var references = _pluginAssociationManager.GetPluginsInProfile(_currentProfileId);

        // 获取当前关联的插件 ID 集合
        var currentPluginIds = references.Select(r => r.PluginId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 获取原始插件列表中被移除的插件（用于市场 Profile）
        var missingOriginalPlugins = _pluginAssociationManager.GetMissingOriginalPlugins(_currentProfileId);

        // 获取缺失插件（已关联但未安装的 + 原始列表中被移除的）
        var missingPlugins = _pluginAssociationManager.GetMissingPlugins(_currentProfileId);
        var totalMissingCount = missingPlugins.Count + missingOriginalPlugins.Count;

        // 显示缺失警告
        if (totalMissingCount > 0)
        {
            MissingWarning.Visibility = Visibility.Visible;
            MissingWarningText.Text = $"{totalMissingCount} 个插件缺失，部分功能可能无法使用";
        }
        else
        {
            MissingWarning.Visibility = Visibility.Collapsed;
        }

        // 转换为视图模型
        var viewModels = references.Select(r => CreatePluginViewModel(r)).ToList();

        // 添加原始列表中被移除的插件（显示为缺失状态）
        foreach (var pluginId in missingOriginalPlugins)
        {
            var vm = CreateMissingOriginalPluginViewModel(pluginId);
            viewModels.Add(vm);
        }

        PluginList.ItemsSource = viewModels;
        PluginCountText.Text = $"({viewModels.Count} 个插件)";
        NoPluginsText.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 为原始列表中被移除的插件创建视图模型
    /// </summary>
    private ProfilePluginViewModel CreateMissingOriginalPluginViewModel(string pluginId)
    {
        var vm = new ProfilePluginViewModel {
            PluginId = pluginId, Enabled = false, Status = PluginInstallStatus.Missing,
            IsRemovedFromOriginal = true // 标记为从原始列表移除
        };

        // 尝试获取插件信息
        var manifest = _pluginLibrary.GetPluginManifest(pluginId);
        if (manifest != null)
        {
            vm.Name = manifest.Name ?? pluginId;
            vm.Version = manifest.Version ?? "1.0.0";
            vm.Description = manifest.Description;
        }
        else
        {
            // 尝试从内置插件获取信息
            vm.Name = pluginId;
            vm.Version = "?";

            var builtInPath = Path.Combine(AppPaths.BuiltInPluginsDirectory, pluginId, "plugin.json");
            var result = PluginManifest.LoadFromFile(builtInPath);
            if (result.IsSuccess && result.Manifest != null)
            {
                vm.Name = result.Manifest.Name ?? pluginId;
                vm.Version = result.Manifest.Version ?? "1.0.0";
                vm.Description = result.Manifest.Description;
            }
        }

        return vm;
    }

    /// <summary>
    /// 创建插件视图模型
    /// </summary>
    private ProfilePluginViewModel CreatePluginViewModel(PluginReference reference)
    {
        var vm = new ProfilePluginViewModel { PluginId = reference.PluginId, Enabled = reference.Enabled,
                                              Status = reference.Status };

        // 获取插件信息
        if (reference.Status == PluginInstallStatus.Installed || reference.Status == PluginInstallStatus.Disabled)
        {
            var manifest = _pluginLibrary.GetPluginManifest(reference.PluginId);
            if (manifest != null)
            {
                vm.Name = manifest.Name ?? reference.PluginId;
                vm.Version = manifest.Version ?? "1.0.0";
                vm.Description = manifest.Description;
            }
            else
            {
                vm.Name = reference.PluginId;
                vm.Version = "?";
            }
        }
        else
        {
            // 缺失的插件，尝试从内置插件获取信息
            vm.Name = reference.PluginId;
            vm.Version = "?";

            var builtInPath = Path.Combine(AppPaths.BuiltInPluginsDirectory, reference.PluginId, "plugin.json");
            var result = PluginManifest.LoadFromFile(builtInPath);
            if (result.IsSuccess && result.Manifest != null)
            {
                vm.Name = result.Manifest.Name ?? reference.PluginId;
                vm.Version = result.Manifest.Version ?? "1.0.0";
                vm.Description = result.Manifest.Description;
            }
        }

        return vm;
    }

    /// <summary>
    /// Profile 选择变化
    /// </summary>
    private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileSelector.SelectedItem is ProfileSelectorViewModel vm)
        {
            _currentProfileId = vm.Id;
            RefreshPluginList();
            UpdateProfileButtons();
        }
    }

    /// <summary>
    /// 更新 Profile 操作按钮状态
    /// </summary>
    private void UpdateProfileButtons()
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            BtnSetCurrent.Visibility = Visibility.Collapsed;
            BtnEditProfile.IsEnabled = false;
            BtnDeleteProfile.IsEnabled = false;
            return;
        }

        // 「设为当前」按钮：当选中的 Profile 不是当前 Profile 时显示
        var isCurrent =
            _currentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase);
        BtnSetCurrent.Visibility = isCurrent ? Visibility.Collapsed : Visibility.Visible;

        // 编辑按钮始终可用
        BtnEditProfile.IsEnabled = true;

        // 删除按钮对默认 Profile 禁用
        var isDefault = _profileManager.IsDefaultProfile(_currentProfileId);
        BtnDeleteProfile.IsEnabled = !isDefault;
    }

    /// <summary>
    /// 设为当前 Profile 按钮点击
    /// </summary>
    private void BtnSetCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            _notificationService.Warning("请先选择一个 Profile");
            return;
        }

        // 检查是否已经是当前 Profile
        if (_currentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase))
        {
            _notificationService.Info("该 Profile 已经是当前使用的 Profile");
            return;
        }

        var profile = _profileManager.GetProfileById(_currentProfileId);
        var profileName = profile?.Name ?? _currentProfileId;

        // 切换 Profile
        var success = _profileManager.SwitchProfile(_currentProfileId);

        if (success)
        {
            // 刷新 UI 以更新「当前使用」标识
            RefreshProfileList();

            _notificationService.Success($"已切换到 Profile \"{profileName}\"");
        }
        else
        {
            _notificationService.Error($"切换到 Profile \"{profileName}\" 失败");
        }
    }

    /// <summary>
    /// 插件启用/禁用切换
    /// </summary>
    private void PluginToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
        {
            var enabled = checkBox.IsChecked ?? false;
            _profileManager.SetPluginEnabled(_currentProfileId, pluginId, enabled);

            // 刷新列表以更新状态显示
            RefreshPluginList();
        }
    }

    /// <summary>
    /// 添加插件按钮点击
    /// </summary>
    private void BtnAddPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            _notificationService.Warning("请先选择一个 Profile");
            return;
        }

        // 获取已安装但未添加到当前 Profile 的插件
        var installedPlugins = _pluginLibrary.GetInstalledPlugins();
        var currentPlugins = _pluginAssociationManager.GetPluginsInProfile(_currentProfileId)
                                 .Select(r => r.PluginId)
                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availablePlugins = installedPlugins.Where(p => !currentPlugins.Contains(p.Id)).ToList();

        if (availablePlugins.Count == 0)
        {
            _notificationService.Info("没有可添加的插件，请先在「已安装插件」页面安装插件");
            return;
        }

        // 显示插件选择对话框
        var dialog = _dialogFactory.CreatePluginSelectorDialog(availablePlugins, _currentProfileId);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            RefreshPluginList();
        }
    }

    /// <summary>
    /// 安装单个缺失插件
    /// </summary>
    private void BtnInstallPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pluginId)
        {
            var result = _pluginLibrary.InstallPlugin(pluginId);
            if (result.IsSuccess)
            {
                RefreshPluginList();
                _notificationService.Success($"插件 \"{pluginId}\" 安装成功");
            }
            else
            {
                _notificationService.Error($"安装失败: {result.ErrorMessage}");
            }
        }
    }

    /// <summary>
    /// 一键安装缺失插件
    /// </summary>
    private void BtnInstallMissing_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
            return;

        // 获取已关联但未安装的插件
        var missingPlugins = _pluginAssociationManager.GetMissingPlugins(_currentProfileId);

        // 获取原始列表中被移除的插件
        var missingOriginalPlugins = _pluginAssociationManager.GetMissingOriginalPlugins(_currentProfileId);

        // 合并所有缺失的插件
        var allMissingPlugins = new HashSet<string>(missingPlugins, StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in missingOriginalPlugins)
        {
            allMissingPlugins.Add(pluginId);
        }

        if (allMissingPlugins.Count == 0)
        {
            _notificationService.Info("没有缺失的插件");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var pluginId in allMissingPlugins)
        {
            // 如果是从原始列表移除的，先添加关联
            if (missingOriginalPlugins.Contains(pluginId))
            {
                _pluginAssociationManager.AddPluginToProfile(pluginId, _currentProfileId);
            }

            // 如果插件未安装，尝试安装
            if (!_pluginLibrary.IsInstalled(pluginId))
            {
                var result = _pluginLibrary.InstallPlugin(pluginId);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            else
            {
                // 插件已安装，只是被移除了关联，算作成功
                successCount++;
            }
        }

        RefreshPluginList();

        // 检查补全的 Profile 是否是当前正在使用的 Profile
        var isCurrentProfile =
            _currentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase);

        if (isCurrentProfile && successCount > 0)
        {
            // 重新加载当前 Profile 的插件，使新安装的插件生效
            _pluginHost.LoadPluginsForProfile(_currentProfileId);
        }

        if (failCount > 0)
        {
            _notificationService.Warning($"安装完成: 成功 {successCount} 个，失败 {failCount} 个");
        }
        else if (isCurrentProfile)
        {
            _notificationService.Success($"成功安装 {successCount} 个插件，已自动加载");
        }
        else
        {
            _notificationService.Success($"成功安装 {successCount} 个插件");
        }
    }

    /// <summary>
    /// 从 Profile 移除插件
    /// </summary>
    private async void BtnRemovePlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
        {
            // 获取插件名称
            var manifest = _pluginLibrary.GetPluginManifest(pluginId);
            var pluginName = manifest?.Name ?? pluginId;

            // 检查是否是市场 Profile（有原始插件列表）
            var hasOriginal = _pluginAssociationManager.HasOriginalPlugins(_currentProfileId);
            var originalPlugins = _pluginAssociationManager.GetOriginalPlugins(_currentProfileId);
            var isInOriginal = originalPlugins.Contains(pluginId, StringComparer.OrdinalIgnoreCase);

            string message;
            if (hasOriginal && isInOriginal)
            {
                message =
                    $"确定要从此 Profile 中移除插件 \"{pluginName}\" 吗？\n\n移除后插件将显示为缺失状态，可以随时重新添加。";
            }
            else
            {
                message =
                    $"确定要从此 Profile 中移除插件 \"{pluginName}\" 吗？\n\n注意：这只会移除引用，不会卸载插件本体。";
            }

            var confirmed = await _notificationService.ConfirmAsync(message, "确认移除");

            if (confirmed)
            {
                _pluginAssociationManager.RemovePluginFromProfile(pluginId, _currentProfileId);
                RefreshPluginList();
                _notificationService.Success($"已从 Profile 中移除插件 \"{pluginName}\"");
            }
        }
    }

    /// <summary>
    /// 打开插件设置（Profile 特定配置）
    /// </summary>
    private void BtnPluginSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
        {
            // 获取插件信息
            var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
            if (pluginInfo == null)
            {
                _notificationService.Error($"找不到插件 {pluginId}");
                return;
            }

            // 获取插件源码目录
            var pluginDirectory = _pluginLibrary.GetPluginDirectory(pluginId);

            // 获取当前 Profile 的配置目录
            var configDirectory = _pluginHost.GetPluginConfigDirectory(_currentProfileId, pluginId);

            // 打开插件设置窗口（传入 profileId）
            PluginSettingsWindow.ShowSettings(pluginId, pluginInfo.Name, pluginDirectory, configDirectory,
                                              Window.GetWindow(this), _currentProfileId);
        }
    }

    /// <summary>
    /// 将插件添加回 Profile（用于从原始列表移除的插件）
    /// </summary>
    private void BtnAddBackPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
        {
            // 获取插件名称
            var manifest = _pluginLibrary.GetPluginManifest(pluginId);
            var pluginName = manifest?.Name ?? pluginId;

            // 添加插件到 Profile
            var added = _pluginAssociationManager.AddPluginToProfile(pluginId, _currentProfileId);

            if (added)
            {
                RefreshPluginList();
                _notificationService.Success($"已将插件 \"{pluginName}\" 添加到 Profile");
            }
            else
            {
                _notificationService.Warning($"插件 \"{pluginName}\" 已在 Profile 中");
            }
        }
    }

    /// <summary>
    /// 新建 Profile 按钮点击
    /// </summary>
    private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = _dialogFactory.CreateProfileCreateDialog();
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && dialog.IsConfirmed && !string.IsNullOrEmpty(dialog.ProfileId))
        {
            // 刷新 Profile 列表
            RefreshProfileList();

            // 选中新创建的 Profile
            SelectProfile(dialog.ProfileId);
        }
    }

    /// <summary>
    /// 编辑 Profile 按钮点击
    /// </summary>
    private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            _notificationService.Warning("请先选择一个 Profile");
            return;
        }

        var profile = _profileManager.GetProfileById(_currentProfileId);
        if (profile == null)
        {
            _notificationService.Error("Profile 不存在");
            return;
        }

        var dialog = _dialogFactory.CreateProfileEditDialog(profile);
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true && dialog.IsConfirmed)
        {
            // 刷新 Profile 列表以显示更新后的名称
            RefreshProfileList();
        }
    }

    /// <summary>
    /// 删除 Profile 按钮点击
    /// </summary>
    private async void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            _notificationService.Warning("请先选择一个 Profile");
            return;
        }

        // 检查是否是默认 Profile
        if (_profileManager.IsDefaultProfile(_currentProfileId))
        {
            _notificationService.Warning("默认 Profile 不能删除");
            return;
        }

        var profile = _profileManager.GetProfileById(_currentProfileId);
        var profileName = profile?.Name ?? _currentProfileId;

        // 显示确认对话框
        var confirmed = await _notificationService.ConfirmAsync(
            $"确定要删除 Profile \"{profileName}\" 吗？\n\n此操作将删除该 Profile 及其所有插件关联，但不会卸载插件本体。",
            "确认删除");

        if (!confirmed)
            return;

        // 执行删除
        var deleteResult = _profileManager.DeleteProfile(_currentProfileId);

        if (deleteResult.IsSuccess)
        {
            // 刷新 Profile 列表（会自动切换到默认 Profile）
            RefreshProfileList();
            _notificationService.Success($"Profile \"{profileName}\" 已删除");
        }
        else
        {
            _notificationService.Error($"删除失败: {deleteResult.ErrorMessage}");
        }
    }

    /// <summary>
    /// 选中指定的 Profile
    /// </summary>
    private void SelectProfile(string profileId)
    {
        if (ProfileSelector.ItemsSource is IEnumerable<ProfileSelectorViewModel> viewModels)
        {
            var vm = viewModels.FirstOrDefault(v => v.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
            if (vm != null)
            {
                ProfileSelector.SelectedItem = vm;
            }
        }
    }

    /// <summary>
    /// 导出 Profile
    /// </summary>
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProfileId))
        {
            _notificationService.Warning("请先选择一个 Profile");
            return;
        }

        var profile = _profileManager.GetProfileById(_currentProfileId);
        if (profile == null)
        {
            _notificationService.Error("Profile 不存在");
            return;
        }

        var dialog =
            new SaveFileDialog { Title = "导出 Profile", Filter = "JSON 文件 (*.json)|*.json",
                                 FileName = $"{profile.Name ?? _currentProfileId}_profile.json", DefaultExt = ".json" };

        if (dialog.ShowDialog() == true)
        {
            var success = _profileManager.ExportProfileToFile(_currentProfileId, dialog.FileName);
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
    /// 导入 Profile
    /// </summary>
    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog { Title = "导入 Profile", Filter = "JSON 文件 (*.json)|*.json", DefaultExt = ".json" };

        if (dialog.ShowDialog() != true)
            return;

        // 加载导入数据
        var data = ProfileExportData.LoadFromFile(dialog.FileName);
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
            RefreshProfileList();

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
}

/// <summary>
/// Profile 选择器视图模型
/// </summary>
public class ProfileSelectorViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCurrent { get; set; } = false;
}

/// <summary>
/// Profile 插件视图模型
/// </summary>
public class ProfilePluginViewModel
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
