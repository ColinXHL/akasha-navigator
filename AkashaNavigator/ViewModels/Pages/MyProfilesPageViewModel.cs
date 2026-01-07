using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.ViewModels.Pages
{
    /// <summary>
    /// 我的 Profile 页面的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class MyProfilesPageViewModel : ObservableObject
    {
        private readonly IProfileManager _profileManager;
        private readonly IPluginAssociationManager _pluginAssociationManager;
        private readonly IPluginLibrary _pluginLibrary;
        private readonly IPluginHost _pluginHost;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// Profile 列表
        /// </summary>
        public ObservableCollection<ProfileSelectorModel> Profiles { get; } = new();

        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<ProfilePluginModel> Plugins { get; } = new();

        /// <summary>
        /// 当前选中的 Profile ID（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetCurrentCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditProfileCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddPluginCommand))]
        [NotifyCanExecuteChangedFor(nameof(InstallMissingCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        private string? _currentProfileId;

        /// <summary>
        /// 当前选中的 Profile（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private ProfileSelectorModel? _selectedProfile;

        /// <summary>
        /// 是否显示设为当前按钮（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private Visibility _setCurrentButtonVisibility = Visibility.Collapsed;

        /// <summary>
        /// 缺失警告可见性（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private Visibility _missingWarningVisibility = Visibility.Collapsed;

        /// <summary>
        /// 缺失警告文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _missingWarningText = string.Empty;

        /// <summary>
        /// 插件数量文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _pluginCountText = "(0 个插件)";

        /// <summary>
        /// 是否无插件（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _hasNoPlugins;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MyProfilesPageViewModel(
            IProfileManager profileManager,
            IPluginAssociationManager pluginAssociationManager,
            IPluginLibrary pluginLibrary,
            IPluginHost pluginHost,
            INotificationService notificationService)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _pluginAssociationManager = pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
            _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
            _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// 页面加载时刷新 Profile 列表
        /// </summary>
        [RelayCommand]
        public void OnLoaded()
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
                                 .Select(p => new ProfileSelectorModel {
                                     Id = p.Id, Name = p.Name ?? p.Id,
                                     IsCurrent = p.Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase)
                                 })
                                 .ToList();

            Profiles.Clear();
            foreach (var vm in viewModels)
            {
                Profiles.Add(vm);
            }

            // 选中当前 Profile
            var currentVm = viewModels.FirstOrDefault(vm => vm.IsCurrent);
            if (currentVm != null)
            {
                SelectedProfile = currentVm;
            }
            else if (viewModels.Count > 0)
            {
                SelectedProfile = viewModels[0];
            }
        }

        /// <summary>
        /// 选中的 Profile 变化时（自动生成的方法）
        /// </summary>
        partial void OnSelectedProfileChanged(ProfileSelectorModel? value)
        {
            if (value != null)
            {
                CurrentProfileId = value.Id;
                RefreshPluginList();
                UpdateProfileButtons();
            }
        }

        /// <summary>
        /// 更新 Profile 操作按钮状态
        /// </summary>
        private void UpdateProfileButtons()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                SetCurrentButtonVisibility = Visibility.Collapsed;
                return;
            }

            // 「设为当前」按钮：当选中的 Profile 不是当前 Profile 时显示
            var isCurrent =
                CurrentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase);
            SetCurrentButtonVisibility = isCurrent ? Visibility.Collapsed : Visibility.Visible;

            // 编辑和删除按钮状态由命令的 CanExecute 方法自动处理
        }

        /// <summary>
        /// 刷新插件清单
        /// </summary>
        public void RefreshPluginList()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                Plugins.Clear();
                HasNoPlugins = true;
                MissingWarningVisibility = Visibility.Collapsed;
                PluginCountText = "(0 个插件)";
                return;
            }

            // 获取 Profile 的插件引用
            var references = _pluginAssociationManager.GetPluginsInProfile(CurrentProfileId);

            // 获取原始插件列表中被移除的插件（用于市场 Profile）
            var missingOriginalPlugins = _pluginAssociationManager.GetMissingOriginalPlugins(CurrentProfileId);

            // 获取缺失插件（已关联但未安装的 + 原始列表中被移除的）
            var missingPlugins = _pluginAssociationManager.GetMissingPlugins(CurrentProfileId);
            var totalMissingCount = missingPlugins.Count + missingOriginalPlugins.Count;

            // 显示缺失警告
            if (totalMissingCount > 0)
            {
                MissingWarningVisibility = Visibility.Visible;
                MissingWarningText = $"{totalMissingCount} 个插件缺失，部分功能可能无法使用";
            }
            else
            {
                MissingWarningVisibility = Visibility.Collapsed;
            }

            // 转换为视图模型
            var viewModels = references.Select(r => CreatePluginModel(r)).ToList();

            // 添加原始列表中被移除的插件（显示为缺失状态）
            foreach (var pluginId in missingOriginalPlugins)
            {
                var vm = CreateMissingOriginalPluginModel(pluginId);
                viewModels.Add(vm);
            }

            Plugins.Clear();
            foreach (var vm in viewModels)
            {
                Plugins.Add(vm);
            }

            PluginCountText = $"({viewModels.Count} 个插件)";
            HasNoPlugins = viewModels.Count == 0;
        }

        /// <summary>
        /// 为原始列表中被移除的插件创建视图模型
        /// </summary>
        private ProfilePluginModel CreateMissingOriginalPluginModel(string pluginId)
        {
            // 根据实际安装状态设置状态（而非硬编码为 Missing）
            var actualStatus = _pluginLibrary.IsInstalled(pluginId)
                ? PluginInstallStatus.Installed
                : PluginInstallStatus.Missing;

            var vm = new ProfilePluginModel {
                PluginId = pluginId, Enabled = actualStatus == PluginInstallStatus.Installed, Status = actualStatus,
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
        private ProfilePluginModel CreatePluginModel(PluginReference reference)
        {
            var vm = new ProfilePluginModel { PluginId = reference.PluginId, Enabled = reference.Enabled,
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
        /// 设为当前 Profile 命令（自动生成 SetCurrentCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSetCurrent))]
        private void SetCurrent()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                _notificationService.Warning("请先选择一个 Profile");
                return;
            }

            // 检查是否已经是当前 Profile
            if (CurrentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                _notificationService.Info("该 Profile 已经是当前使用的 Profile");
                return;
            }

            var profile = _profileManager.GetProfileById(CurrentProfileId);
            var profileName = profile?.Name ?? CurrentProfileId;

            // 切换 Profile
            var success = _profileManager.SwitchProfile(CurrentProfileId);

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
        /// 是否可以设为当前（有选中的 Profile 且不是当前 Profile）
        /// </summary>
        private bool CanSetCurrent() => !string.IsNullOrEmpty(CurrentProfileId);

        /// <summary>
        /// 新建 Profile 命令（自动生成 NewProfileCommand）
        /// </summary>
        [RelayCommand]
        private void NewProfile()
        {
            NewProfileRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 编辑 Profile 命令（自动生成 EditProfileCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanEditProfile))]
        private void EditProfile()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                _notificationService.Warning("请先选择一个 Profile");
                return;
            }

            var profile = _profileManager.GetProfileById(CurrentProfileId);
            if (profile == null)
            {
                _notificationService.Error("Profile 不存在");
                return;
            }

            EditProfileRequested?.Invoke(this, profile);
        }

        /// <summary>
        /// 删除 Profile 命令（自动生成 DeleteProfileCommand）
        /// 注意：此命令需要由 Code-behind 处理对话框显示
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
        private void DeleteProfile()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                _notificationService.Warning("请先选择一个 Profile");
                return;
            }

            // 检查是否是默认 Profile
            if (_profileManager.IsDefaultProfile(CurrentProfileId))
            {
                _notificationService.Warning("默认 Profile 不能删除");
                return;
            }

            // 此命令由 Code-behind 的 DeleteProfileRequested 事件处理
            DeleteProfileRequested?.Invoke(this, CurrentProfileId);
        }

        /// <summary>
        /// 是否可以编辑 Profile（有选中的 Profile）
        /// </summary>
        private bool CanEditProfile() => !string.IsNullOrEmpty(CurrentProfileId);

        /// <summary>
        /// 是否可以删除 Profile（有选中的 Profile 且不是默认 Profile）
        /// </summary>
        private bool CanDeleteProfile()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
                return false;
            return !_profileManager.IsDefaultProfile(CurrentProfileId);
        }

        /// <summary>
        /// 添加插件命令（自动生成 AddPluginCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddPlugin))]
        private void AddPlugin()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                _notificationService.Warning("请先选择一个 Profile");
                return;
            }

            AddPluginRequested?.Invoke(this, CurrentProfileId);
        }

        /// <summary>
        /// 是否可以添加插件（有选中的 Profile）
        /// </summary>
        private bool CanAddPlugin() => !string.IsNullOrEmpty(CurrentProfileId);

        /// <summary>
        /// 一键安装缺失插件命令（自动生成 InstallMissingCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanInstallMissing))]
        private System.Threading.Tasks.Task InstallMissing()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
                return System.Threading.Tasks.Task.CompletedTask;

            // 获取已关联但未安装的插件
            var missingPlugins = _pluginAssociationManager.GetMissingPlugins(CurrentProfileId);

            // 获取原始列表中被移除的插件
            var missingOriginalPlugins = _pluginAssociationManager.GetMissingOriginalPlugins(CurrentProfileId);

            // 合并所有缺失的插件
            var allMissingPlugins = new HashSet<string>(missingPlugins, StringComparer.OrdinalIgnoreCase);
            foreach (var pluginId in missingOriginalPlugins)
            {
                allMissingPlugins.Add(pluginId);
            }

            if (allMissingPlugins.Count == 0)
            {
                _notificationService.Info("没有缺失的插件");
                return System.Threading.Tasks.Task.CompletedTask;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var pluginId in allMissingPlugins)
            {
                // 如果是从原始列表移除的，先添加关联
                if (missingOriginalPlugins.Contains(pluginId))
                {
                    _pluginAssociationManager.AddPluginToProfile(pluginId, CurrentProfileId);
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
                CurrentProfileId.Equals(_profileManager.CurrentProfile.Id, StringComparison.OrdinalIgnoreCase);

            if (isCurrentProfile && successCount > 0)
            {
                // 重新加载当前 Profile 的插件，使新安装的插件生效
                _pluginHost.LoadPluginsForProfile(CurrentProfileId);
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

            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// 是否可以安装缺失插件
        /// </summary>
        private bool CanInstallMissing() => !string.IsNullOrEmpty(CurrentProfileId);

        /// <summary>
        /// 插件启用/禁用切换
        /// </summary>
        public void TogglePluginEnabled(string pluginId, bool enabled)
        {
            if (!string.IsNullOrEmpty(CurrentProfileId))
            {
                _profileManager.SetPluginEnabled(CurrentProfileId, pluginId, enabled);

                // 刷新列表以更新状态显示
                RefreshPluginList();
            }
        }

        /// <summary>
        /// 安装单个缺失插件命令（自动生成 InstallPluginCommand）
        /// </summary>
        [RelayCommand]
        private void InstallPlugin(string? pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;

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

        /// <summary>
        /// 从 Profile 移除插件命令（自动生成 RemovePluginCommand）
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task RemovePlugin(string? pluginId)
        {
            if (string.IsNullOrEmpty(CurrentProfileId) || string.IsNullOrWhiteSpace(pluginId))
                return;

            // 获取插件名称
            var manifest = _pluginLibrary.GetPluginManifest(pluginId);
            var pluginName = manifest?.Name ?? pluginId;

            // 检查是否是市场 Profile（有原始插件列表）
            var hasOriginal = _pluginAssociationManager.HasOriginalPlugins(CurrentProfileId);
            var originalPlugins = _pluginAssociationManager.GetOriginalPlugins(CurrentProfileId);
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
                _pluginAssociationManager.RemovePluginFromProfile(pluginId, CurrentProfileId);
                RefreshPluginList();
                _notificationService.Success($"已从 Profile 中移除插件 \"{pluginName}\"");
            }
        }

        /// <summary>
        /// 将插件添加回 Profile 命令（自动生成 AddBackPluginCommand）
        /// </summary>
        [RelayCommand]
        private void AddBackPlugin(string? pluginId)
        {
            if (string.IsNullOrEmpty(CurrentProfileId) || string.IsNullOrWhiteSpace(pluginId))
                return;

            // 获取插件名称
            var manifest = _pluginLibrary.GetPluginManifest(pluginId);
            var pluginName = manifest?.Name ?? pluginId;

            // 添加插件到 Profile
            var added = _pluginAssociationManager.AddPluginToProfile(pluginId, CurrentProfileId);

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

        /// <summary>
        /// 导出 Profile 命令（自动生成 ExportCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExport))]
        private void Export()
        {
            if (string.IsNullOrEmpty(CurrentProfileId))
            {
                _notificationService.Warning("请先选择一个 Profile");
                return;
            }

            var profile = _profileManager.GetProfileById(CurrentProfileId);
            if (profile == null)
            {
                _notificationService.Error("Profile 不存在");
                return;
            }

            ExportRequested?.Invoke(this, profile);
        }

        /// <summary>
        /// 是否可以导出
        /// </summary>
        private bool CanExport() => !string.IsNullOrEmpty(CurrentProfileId);

        /// <summary>
        /// 导入 Profile 命令（自动生成 ImportCommand）
        /// </summary>
        [RelayCommand]
        private void Import()
        {
            ImportRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 打开插件设置命令（自动生成 OpenPluginSettingsCommand）
        /// </summary>
        [RelayCommand]
        private void OpenPluginSettings(string? pluginId)
        {
            if (string.IsNullOrEmpty(pluginId) || string.IsNullOrEmpty(CurrentProfileId))
                return;

            OpenPluginSettingsRequested?.Invoke(this, pluginId);
        }

        /// <summary>
        /// 新建 Profile 请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler? NewProfileRequested;

        /// <summary>
        /// 编辑 Profile 请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<GameProfile>? EditProfileRequested;

        /// <summary>
        /// 删除 Profile 请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<string>? DeleteProfileRequested;

        /// <summary>
        /// 添加插件请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<string>? AddPluginRequested;

        /// <summary>
        /// 导出请求事件（由 Code-behind 订阅以显示文件保存对话框）
        /// </summary>
        public event EventHandler<GameProfile>? ExportRequested;

        /// <summary>
        /// 导入请求事件（由 Code-behind 订阅以显示文件打开对话框）
        /// </summary>
        public event EventHandler? ImportRequested;

        /// <summary>
        /// 打开插件设置请求事件（由 Code-behind 订阅）
        /// </summary>
        public event EventHandler<string?>? OpenPluginSettingsRequested;
    }
}
