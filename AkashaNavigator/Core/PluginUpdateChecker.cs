using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 插件更新检查器
    /// 负责检查并提示插件更新
    /// </summary>
    public class PluginUpdateChecker
    {
        private readonly IPluginLibrary _pluginLibrary;
        private readonly ProfileMarketplaceService _profileMarketplaceService;
        private readonly INotificationService _notificationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;
        private readonly PlayerWindow _playerWindow;
        private readonly AppConfig _config;

        /// <summary>
        /// 初始化 PluginUpdateChecker
        /// </summary>
        public PluginUpdateChecker(
            IPluginLibrary pluginLibrary,
            ProfileMarketplaceService profileMarketplaceService,
            INotificationService notificationService,
            IServiceProvider serviceProvider,
            IEventBus eventBus,
            PlayerWindow playerWindow,
            AppConfig config)
        {
            _pluginLibrary = pluginLibrary;
            _profileMarketplaceService = profileMarketplaceService;
            _notificationService = notificationService;
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
            _playerWindow = playerWindow;
            _config = config;
        }

        /// <summary>
        /// 设置插件更新检查
        /// WebView 首次加载完成后检查插件更新（非首次启动且启用了更新提示）
        /// </summary>
        public void SetupUpdateCheck()
        {
            if (!_config.IsFirstLaunch && _config.EnablePluginUpdateNotification)
            {
                // 使用一次性事件处理器订阅 EventBus
                Action<NavigationStateChangedEvent>? handler = null;
                handler = e =>
                {
                    if (handler != null)
                    {
                        _eventBus.Unsubscribe(handler);
                    }
                    // 延迟一小段时间再显示，确保窗口完全加载
                    _playerWindow.Dispatcher.BeginInvoke(
                        new Action(async () => await CheckAndPromptUpdatesAsync()),
                        System.Windows.Threading.DispatcherPriority.Background);
                };
                _eventBus.Subscribe(handler);
            }
        }

        /// <summary>
        /// 检查并提示插件/Profile 更新
        /// </summary>
        private async System.Threading.Tasks.Task CheckAndPromptUpdatesAsync()
        {
            try
            {
                var updates = _pluginLibrary.CheckAllUpdates();
                var continueCheckingProfiles = true;

                if (updates.Count > 0)
                {
                    var dialogFactory = _serviceProvider.GetRequiredService<IDialogFactory>();
                    var dialog = dialogFactory.CreatePluginUpdatePromptDialog(updates);
                    var result = dialog.ShowDialog();

                    if (result == true)
                    {
                        continueCheckingProfiles = HandlePluginUpdateDialogResult(dialog, updates);
                    }
                }

                if (continueCheckingProfiles)
                {
                    await CheckAndPromptProfileUpdatesAsync();
                }
            }
            catch (Exception ex)
            {
                var logService = _serviceProvider.GetRequiredService<ILogService>();
                logService.Error(nameof(PluginUpdateChecker), ex, "检查插件更新时发生异常");
            }
        }

        /// <summary>
        /// 处理更新对话框结果
        /// </summary>
        private bool HandlePluginUpdateDialogResult(PluginUpdatePromptDialog dialog,
                                                    System.Collections.Generic.List<UpdateCheckResult> updates)
        {
            switch (dialog.Result)
            {
                case PluginUpdatePromptResult.OpenPluginCenter:
                    OpenPluginCenter();
                    return false;

                case PluginUpdatePromptResult.UpdateAll:
                    UpdateAllPlugins(updates);
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 检查并提示 Profile 更新
        /// </summary>
        private async System.Threading.Tasks.Task CheckAndPromptProfileUpdatesAsync()
        {
            var updates = await _profileMarketplaceService.CheckAllUpdatesAsync();
            if (updates.Count == 0)
                return;

            var dialogFactory = _serviceProvider.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateProfileUpdatePromptDialog(updates);
            var result = dialog.ShowDialog();

            if (result != true)
                return;

            switch (dialog.Result)
            {
                case ProfileUpdatePromptResult.OpenPluginCenter:
                    OpenProfileCenter();
                    break;
                case ProfileUpdatePromptResult.UpdateAll:
                    await UpdateAllProfilesAsync(updates);
                    break;
            }
        }

        /// <summary>
        /// 打开插件中心
        /// </summary>
        private void OpenPluginCenter()
        {
            // 延迟打开插件中心（等待主窗口创建完成）
            _playerWindow.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    var pluginCenterWindow = _serviceProvider.GetRequiredService<PluginCenterWindow>();
                    pluginCenterWindow.Owner = _playerWindow;
                    // 导航到已安装插件页面
                    pluginCenterWindow.NavigateToInstalledPlugins();
                    pluginCenterWindow.ShowDialog();
                }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 打开插件中心并跳转到 Profile 市场页面
        /// </summary>
        private void OpenProfileCenter()
        {
            _playerWindow.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    var pluginCenterWindow = _serviceProvider.GetRequiredService<PluginCenterWindow>();
                    pluginCenterWindow.Owner = _playerWindow;
                    pluginCenterWindow.NavigateToProfileMarket();
                    pluginCenterWindow.ShowDialog();
                }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 执行一键更新所有插件
        /// </summary>
        private void UpdateAllPlugins(System.Collections.Generic.List<UpdateCheckResult> updates)
        {
            var successCount = 0;
            var failCount = 0;
            foreach (var update in updates)
            {
                var updateResult = _pluginLibrary.UpdatePlugin(update.PluginId);
                if (updateResult.IsSuccess)
                    successCount++;
                else
                    failCount++;
            }

            // 显示更新结果
            if (failCount == 0)
            {
                _notificationService.Success($"成功更新 {successCount} 个插件！", "更新完成");
            }
            else
            {
                _notificationService.Warning($"更新完成：{successCount} 个成功，{failCount} 个失败。", "更新完成");
            }
        }

        /// <summary>
        /// 执行一键更新所有 Profile
        /// </summary>
        private async System.Threading.Tasks.Task UpdateAllProfilesAsync(System.Collections.Generic.List<ProfileUpdateCheckResult> updates)
        {
            var successCount = 0;
            var failCount = 0;

            foreach (var update in updates)
            {
                var updateResult = await _profileMarketplaceService.InstallProfileAsync(update.Profile, overwrite: true);
                if (updateResult.IsSuccess)
                    successCount++;
                else
                    failCount++;
            }

            _eventBus.Publish(new ProfileListChangedEvent());

            if (failCount == 0)
            {
                _notificationService.Success($"成功更新 {successCount} 个 Profile！", "更新完成");
            }
            else
            {
                _notificationService.Warning($"更新完成：{successCount} 个成功，{failCount} 个失败。", "更新完成");
            }
        }
    }
}
