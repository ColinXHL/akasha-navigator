using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;

namespace AkashaNavigator.ViewModels.Pages
{
    /// <summary>
    /// 市场 Profile 视图模型
    /// 用于在 Profile 市场页面显示 Profile 列表项
    /// </summary>
    public partial class MarketplaceProfileViewModel : ObservableObject
    {
        private readonly ProfileMarketplaceService _profileMarketplaceService;

        public MarketplaceProfile Profile { get; }
        private bool _isInstalled;

        /// <summary>
        /// DI容器注入的构造函数
        /// </summary>
        public MarketplaceProfileViewModel(MarketplaceProfile profile, ProfileMarketplaceService profileMarketplaceService)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _profileMarketplaceService = profileMarketplaceService ?? throw new ArgumentNullException(nameof(profileMarketplaceService));
            // 初始化时检查安装状态
            _isInstalled = _profileMarketplaceService.ProfileExists(profile.Id);
        }

        public string Id => Profile.Id;
        public string Name => Profile.Name;
        public string Description => Profile.Description;
        public string Author => Profile.Author;
        public string TargetGame => Profile.TargetGame;
        public string Version => Profile.Version;
        public int PluginCount => Profile.PluginCount;

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);
        public bool HasTargetGame => !string.IsNullOrWhiteSpace(TargetGame);

        /// <summary>
        /// Profile 是否已安装
        /// </summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged(nameof(IsInstalled));
                    OnPropertyChanged(nameof(CanUninstall));
                    OnPropertyChanged(nameof(IsDefaultAndInstalled));
                }
            }
        }

        /// <summary>
        /// 是否是默认 Profile
        /// </summary>
        public bool IsDefaultProfile => Id.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 是否可以卸载（已安装且非默认 Profile）
        /// </summary>
        public bool CanUninstall => IsInstalled && !IsDefaultProfile;

        /// <summary>
        /// 是否是已安装的默认 Profile（用于显示"已安装"标签）
        /// </summary>
        public bool IsDefaultAndInstalled => IsInstalled && IsDefaultProfile;
    }

    /// <summary>
    /// Profile 市场页面的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class ProfileMarketPageViewModel : ObservableObject
    {
        private readonly ProfileMarketplaceService _profileMarketplaceService;
        private readonly IPluginLibrary _pluginLibrary;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// Profile 列表
        /// </summary>
        public ObservableCollection<MarketplaceProfileViewModel> Profiles { get; } = new();

        /// <summary>
        /// 搜索文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// 是否正在加载（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        private bool _isLoading;

        /// <summary>
        /// 错误消息（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string? _errorMessage;

        /// <summary>
        /// 是否显示错误（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _hasError;

        /// <summary>
        /// 是否无可用 Profile（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _hasNoProfiles;

        /// <summary>
        /// 是否无搜索结果（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _hasNoResults;

        /// <summary>
        /// 所有 Profile 列表（用于过滤）
        /// </summary>
        private List<MarketplaceProfileViewModel> _allProfiles = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        public ProfileMarketPageViewModel(
            ProfileMarketplaceService profileMarketplaceService,
            IPluginLibrary pluginLibrary,
            INotificationService notificationService)
        {
            _profileMarketplaceService = profileMarketplaceService ?? throw new ArgumentNullException(nameof(profileMarketplaceService));
            _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// 搜索文本变化时（自动生成的方法）
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            FilterProfiles();
        }

        /// <summary>
        /// 刷新命令（自动生成 RefreshCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private async Task RefreshAsync()
        {
            await LoadProfilesAsync();
        }

        /// <summary>
        /// 是否可以刷新（不在加载中）
        /// </summary>
        private bool CanRefresh() => !IsLoading;

        /// <summary>
        /// 打开订阅源管理对话框命令（自动生成 ManageSourcesCommand）
        /// </summary>
        [RelayCommand]
        private void ManageSources()
        {
            ManageSourcesRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 加载市场 Profile 列表
        /// </summary>
        public async Task LoadProfilesAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            HasError = false;
            HasNoProfiles = false;
            HasNoResults = false;
            ErrorMessage = null;
            Profiles.Clear();

            try
            {
                // 获取 Profile 列表（包含内置 Profile 和订阅源 Profile）
                var profiles = await _profileMarketplaceService.FetchAvailableProfilesAsync();

                // 如果没有任何 Profile（内置和订阅源都没有）
                if (profiles.Count == 0)
                {
                    HasNoProfiles = true;
                    IsLoading = false;
                    return;
                }

                // 转换为视图模型
                _allProfiles = profiles.Select(p => new MarketplaceProfileViewModel(p, _profileMarketplaceService)).ToList();

                // 应用过滤
                FilterProfiles();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载失败: {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 过滤 Profile 列表
        /// </summary>
        private void FilterProfiles()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            List<MarketplaceProfileViewModel> filtered;
            if (string.IsNullOrEmpty(query))
            {
                filtered = _allProfiles;
            }
            else
            {
                var lowerQuery = query.ToLowerInvariant();
                filtered = _allProfiles
                               .Where(p => (p.Name?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                                           (p.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                                           (p.TargetGame?.ToLowerInvariant().Contains(lowerQuery) ?? false))
                               .ToList();
            }

            Profiles.Clear();
            foreach (var vm in filtered)
            {
                Profiles.Add(vm);
            }

            HasNoResults = filtered.Count == 0 && _allProfiles.Count > 0;
        }

        /// <summary>
        /// 显示 Profile 详情命令（自动生成 ShowDetailsCommand）
        /// </summary>
        [RelayCommand]
        private void ShowDetails(MarketplaceProfileViewModel? vm)
        {
            if (vm != null)
            {
                ShowProfileDetailsRequested?.Invoke(this, vm);
            }
        }

        /// <summary>
        /// 安装 Profile 命令（自动生成 InstallCommand）
        /// </summary>
        [RelayCommand]
        private async Task InstallAsync(MarketplaceProfileViewModel? vm)
        {
            if (vm == null)
                return;

            await InstallProfileAsync(vm.Profile);
        }

        /// <summary>
        /// 卸载 Profile 命令（自动生成 UninstallCommand）
        /// </summary>
        [RelayCommand]
        private async Task UninstallAsync(MarketplaceProfileViewModel? vm)
        {
            if (vm == null)
                return;

            await UninstallProfileWithDialogAsync(vm);
        }

        /// <summary>
        /// 带对话框的 Profile 卸载流程
        /// </summary>
        private async Task UninstallProfileWithDialogAsync(MarketplaceProfileViewModel vm)
        {
            var profileId = vm.Id;
            var profileName = vm.Name;

            // 获取唯一插件列表
            var uniquePluginIds = _profileMarketplaceService.GetUniquePlugins(profileId);

            List<string>? pluginsToUninstall = null;

            if (uniquePluginIds.Count > 0)
            {
                // 有唯一插件，触发事件显示 PluginUninstallDialog
                var args = new ProfileUninstallEventArgs(profileName, profileId, uniquePluginIds);
                UninstallProfileRequested?.Invoke(this, args);

                if (!args.Confirmed)
                {
                    // 用户取消
                    return;
                }

                pluginsToUninstall = args.SelectedPluginIds;
            }
            else
            {
                // 没有唯一插件，使用 NotificationService 显示确认对话框
                var confirmed = await _notificationService.ConfirmAsync(
                    $"确定要卸载 Profile \"{profileName}\" 吗？\n\n此操作将删除该 Profile 的配置文件。", "确认卸载");

                if (!confirmed)
                {
                    return;
                }
            }

            // 执行卸载
            await UninstallProfileAsync(vm, pluginsToUninstall);
        }

        /// <summary>
        /// 执行 Profile 卸载
        /// </summary>
        private async Task UninstallProfileAsync(MarketplaceProfileViewModel vm, List<string>? pluginsToUninstall)
        {
            var profileId = vm.Id;
            var profileName = vm.Name;

            // 调用服务执行卸载
            var result = _profileMarketplaceService.UninstallProfile(profileId, pluginsToUninstall);

            if (result.IsSuccess)
            {
                // 强制刷新 ViewModel 的 IsInstalled 属性（从服务重新检查状态）
                vm.IsInstalled = _profileMarketplaceService.ProfileExists(profileId);

                // 刷新列表以更新 UI 状态
                FilterProfiles();

                // 构建成功消息
                var message = $"Profile \"{profileName}\" 已成功卸载。";

                if (result.UninstalledPlugins.Count > 0)
                {
                    message += $"\n\n已卸载 {result.UninstalledPlugins.Count} 个插件。";
                }

                if (result.FailedPlugins.Count > 0)
                {
                    message += $"\n\n⚠ {result.FailedPlugins.Count} 个插件卸载失败：\n";
                    message += string.Join("\n", result.FailedPlugins.Take(5).Select(p => $"  • {p}"));
                    if (result.FailedPlugins.Count > 5)
                    {
                        message += $"\n  ... 等 {result.FailedPlugins.Count} 个";
                    }
                }

                _notificationService.Success(message, "卸载成功");
            }
            else
            {
                _notificationService.Error($"卸载失败: {result.ErrorMessage}", "卸载失败");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 安装 Profile
        /// </summary>
        private async Task InstallProfileAsync(MarketplaceProfile profile)
        {
            // 检测缺失插件
            var missingPlugins = _profileMarketplaceService.GetMissingPlugins(profile);

            // 检查是否已存在
            bool overwrite = false;
            if (_profileMarketplaceService.ProfileExists(profile.Id))
            {
                var confirmed = await _notificationService.ConfirmAsync(
                    $"Profile \"{profile.Name}\" 已存在。\n\n是否覆盖现有 Profile？", "Profile 已存在");

                if (!confirmed)
                    return;

                overwrite = true;
            }
            else if (missingPlugins.Count > 0)
            {
                // 提示缺失插件
                var message = $"即将安装 Profile: {profile.Name}\n\n";
                message += $"⚠ {missingPlugins.Count} 个插件缺失:\n";
                message += string.Join("\n", missingPlugins.Take(5).Select(p => $"  • {p}"));
                if (missingPlugins.Count > 5)
                {
                    message += $"\n  ... 等 {missingPlugins.Count} 个";
                }
                message += "\n\n安装后可以在「我的 Profile」页面一键安装缺失插件。\n\n是否继续？";

                var confirmed = await _notificationService.ConfirmAsync(message, "确认安装");
                if (!confirmed)
                    return;
            }

            // 执行安装
            var installResult = await _profileMarketplaceService.InstallProfileAsync(profile, overwrite);

            if (installResult.IsSuccess)
            {
                // 强制刷新 ViewModel 的 IsInstalled 属性
                var vm = _allProfiles.FirstOrDefault(p => p.Id == profile.Id);
                if (vm != null)
                {
                    vm.IsInstalled = _profileMarketplaceService.ProfileExists(profile.Id);
                }

                // 刷新列表显示
                FilterProfiles();

                var successMessage = $"Profile \"{profile.Name}\" 安装成功！";
                if (installResult.MissingPlugins.Count > 0)
                {
                    successMessage +=
                        $"\n\n有 {installResult.MissingPlugins.Count} 个插件缺失，可以在「我的 Profile」页面点击「一键安装缺失插件」进行安装。";
                }

                _notificationService.Success(successMessage, "安装成功");
            }
            else
            {
                _notificationService.Error($"安装失败: {installResult.ErrorMessage}", "安装失败");
            }
        }

        /// <summary>
        /// 订阅源管理请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler? ManageSourcesRequested;

        /// <summary>
        /// 显示 Profile 详情请求事件（由 Code-behind 订阅以显示对话框）
        /// </summary>
        public event EventHandler<MarketplaceProfileViewModel>? ShowProfileDetailsRequested;

        /// <summary>
        /// 卸载 Profile 请求事件（由 Code-behind 订阅以显示卸载对话框）
        /// </summary>
        public event EventHandler<ProfileUninstallEventArgs>? UninstallProfileRequested;

        /// <summary>
        /// 获取已安装插件信息（供 Code-behind 调用）
        /// </summary>
        public InstalledPluginInfo? GetInstalledPluginInfo(string pluginId)
        {
            return _pluginLibrary.GetInstalledPluginInfo(pluginId);
        }
    }

    /// <summary>
    /// Profile 卸载事件参数
    /// </summary>
    public class ProfileUninstallEventArgs : EventArgs
    {
        /// <summary>
        /// Profile 名称
        /// </summary>
        public string ProfileName { get; }

        /// <summary>
        /// Profile ID
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// 唯一插件 ID 列表
        /// </summary>
        public List<string> UniquePluginIds { get; }

        /// <summary>
        /// 用户是否确认卸载
        /// </summary>
        public bool Confirmed { get; set; }

        /// <summary>
        /// 用户选择要卸载的插件 ID 列表
        /// </summary>
        public List<string>? SelectedPluginIds { get; set; }

        public ProfileUninstallEventArgs(string profileName, string profileId, List<string> uniquePluginIds)
        {
            ProfileName = profileName;
            ProfileId = profileId;
            UniquePluginIds = uniquePluginIds;
        }
    }
}
