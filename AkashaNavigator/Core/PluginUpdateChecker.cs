using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
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
        private readonly PluginLibrary _pluginLibrary;
        private readonly INotificationService _notificationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly PlayerWindow _playerWindow;
        private readonly AppConfig _config;

        /// <summary>
        /// 初始化 PluginUpdateChecker
        /// </summary>
        public PluginUpdateChecker(
            PluginLibrary pluginLibrary,
            INotificationService notificationService,
            IServiceProvider serviceProvider,
            PlayerWindow playerWindow,
            AppConfig config)
        {
            _pluginLibrary = pluginLibrary;
            _notificationService = notificationService;
            _serviceProvider = serviceProvider;
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
                // 使用一次性事件处理器
                EventHandler? handler = null;
                handler = (s, e) =>
                {
                    _playerWindow.NavigationStateChanged -= handler;
                    // 延迟一小段时间再显示，确保窗口完全加载
                    _playerWindow.Dispatcher.BeginInvoke(
                        new Action(CheckAndPromptPluginUpdates),
                        System.Windows.Threading.DispatcherPriority.Background);
                };
                _playerWindow.NavigationStateChanged += handler;
            }
        }

        /// <summary>
        /// 检查并提示插件更新
        /// </summary>
        private void CheckAndPromptPluginUpdates()
        {
            try
            {
                var updates = _pluginLibrary.CheckAllUpdates();
                if (updates.Count == 0)
                    return;

                var dialogFactory = _serviceProvider.GetRequiredService<IDialogFactory>();
                var dialog = dialogFactory.CreatePluginUpdatePromptDialog(updates);
                var result = dialog.ShowDialog();

                if (result == true)
                {
                    HandleUpdateDialogResult(dialog, updates);
                }
            }
            catch (Exception ex)
            {
                var logService = _serviceProvider.GetRequiredService<ILogService>();
                logService.Error("PluginUpdateChecker", ex, "检查插件更新时发生异常");
            }
        }

        /// <summary>
        /// 处理更新对话框结果
        /// </summary>
        private void HandleUpdateDialogResult(PluginUpdatePromptDialog dialog, System.Collections.Generic.List<UpdateCheckResult> updates)
        {
            switch (dialog.Result)
            {
                case PluginUpdatePromptResult.OpenPluginCenter:
                    OpenPluginCenter();
                    break;

                case PluginUpdatePromptResult.UpdateAll:
                    UpdateAllPlugins(updates);
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
                    var pluginCenterWindow = new PluginCenterWindow();
                    pluginCenterWindow.Owner = _playerWindow;
                    // 导航到已安装插件页面
                    pluginCenterWindow.NavigateToInstalledPlugins();
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
    }
}
