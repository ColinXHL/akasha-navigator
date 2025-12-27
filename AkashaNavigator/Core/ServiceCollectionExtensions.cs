using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Pages;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 依赖注入容器配置扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 配置应用程序的所有服务
        /// 注册顺序按依赖层级：Level 0 → Level 1 → Level 2 → Level 3
        /// </summary>
        public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
        {
            // ============================================================
            // Level 0: 无依赖服务
            // ============================================================

            // LogService（无依赖）
            services.AddSingleton<ILogService, LogService>();

            // CursorDetectionService（无依赖）
            services.AddSingleton<ICursorDetectionService, CursorDetectionService>();

            // PluginRegistry（依赖LogService）
            services.AddSingleton<IPluginRegistry, PluginRegistry>();

            // ProfileRegistry（依赖LogService）
            services.AddSingleton<IProfileRegistry, ProfileRegistry>();

            // PluginLibrary（无依赖）
            services.AddSingleton<IPluginLibrary, PluginLibrary>();
            services.AddSingleton<PluginLibrary, PluginLibrary>();

            // HotkeyService（无依赖，使用Win32钩子）
            services.AddSingleton<HotkeyService>();

            // ============================================================
            // Level 1: 依赖 LogService
            // ============================================================

            // ConfigService（依赖LogService）
            services.AddSingleton<IConfigService, ConfigService>();

            // NotificationService（依赖LogService）
            services.AddSingleton<INotificationService, NotificationService>();

            // SubtitleService（依赖LogService）
            services.AddSingleton<ISubtitleService, SubtitleService>();

            // SubscriptionManager（依赖LogService + ProfileRegistry + PluginRegistry）
            services.AddSingleton<ISubscriptionManager, SubscriptionManager>();

            // DataMigration（依赖LogService）
            services.AddSingleton<DataMigration>();

            // PluginAssociationManager（依赖LogService + PluginLibrary）
            services.AddSingleton<IPluginAssociationManager, PluginAssociationManager>();

            // ============================================================
            // Level 2: 依赖 LogService + ProfileManager（复杂依赖）
            // ============================================================

            // PluginHost（依赖LogService + PluginAssociationManager + PluginLibrary）
            services.AddSingleton<IPluginHost, PluginHost>();

            // ProfileManager（依赖ConfigService, LogService, PluginHost, PluginAssociationManager, SubscriptionManager, PluginLibrary, ProfileRegistry）
            services.AddSingleton<IProfileManager, ProfileManager>();

            // ============================================================
            // Level 3: 依赖 LogService + ProfileManager（必须在ProfileManager之后注册）
            // ============================================================

            // WindowStateService（依赖LogService + ProfileManager）
            services.AddSingleton<IWindowStateService, WindowStateService>();

            // PioneerNoteService（依赖LogService + ProfileManager）
            services.AddSingleton<IPioneerNoteService, PioneerNoteService>();

            // DataService（依赖LogService + ProfileManager，必须在ProfileManager之后）
            services.AddSingleton<IDataService, DataService>();

            // ProfileMarketplaceService（依赖LogService + ProfileManager + PluginAssociationManager + PluginLibrary）
            services.AddSingleton<ProfileMarketplaceService>();

            // ============================================================
            // 其他服务
            // ============================================================

            // OverlayManager（无依赖）
            services.AddSingleton<IOverlayManager, OverlayManager>();

            // ============================================================
            // 窗口（Transient，每次请求创建新实例）
            // ============================================================

            // PlayerWindow（依赖所有服务）
            services.AddTransient<PlayerWindow>();

            // SettingsWindow（依赖IConfigService, IProfileManager, INotificationService）
            services.AddTransient<SettingsWindow>();

            // MyProfilesPage（依赖IProfileManager, IPluginAssociationManager, PluginLibrary, IPluginHost, INotificationService）
            services.AddTransient<MyProfilesPage>();

            // InstalledPluginsPage（依赖PluginLibrary, IPluginAssociationManager, INotificationService）
            services.AddTransient<InstalledPluginsPage>();

            // PluginSettingsWindow 工厂方法（用于创建带参数的窗口）
            services.AddSingleton<Func<string, string, string, string, string?, PluginSettingsWindow>>(sp =>
            {
                return (pluginId, pluginName, pluginDirectory, configDirectory, profileId) =>
                {
                    var profileManager = sp.GetRequiredService<IProfileManager>();
                    var logService = sp.GetRequiredService<ILogService>();
                    var pluginHost = sp.GetRequiredService<IPluginHost>();
                    var notificationService = sp.GetRequiredService<INotificationService>();
                    var overlayManager = sp.GetRequiredService<IOverlayManager>();
                    return new PluginSettingsWindow(profileManager, logService, pluginHost, notificationService, overlayManager,
                                                    pluginId, pluginName, pluginDirectory, configDirectory, profileId);
                };
            });

            // HistoryWindow（依赖IDataService）
            services.AddTransient<HistoryWindow>();

            // BookmarkPopup（依赖IDataService）
            services.AddTransient<BookmarkPopup>();

            // RecordNoteDialog 工厂方法（依赖IPioneerNoteService + url, title参数 + PioneerNoteWindow工厂）
            services.AddSingleton<Func<string, string, RecordNoteDialog>>(sp =>
            {
                return (url, title) =>
                {
                    var pioneerNoteService = sp.GetRequiredService<IPioneerNoteService>();
                    var pioneerNoteWindowFactory = () => sp.GetRequiredService<PioneerNoteWindow>();
                    return new RecordNoteDialog(pioneerNoteService, url, title, pioneerNoteWindowFactory);
                };
            });

            // PioneerNoteWindow（依赖IPioneerNoteService）
            services.AddTransient<PioneerNoteWindow>();

            // ============================================================
            // Dialogs（Transient，每次请求创建新实例）
            // ============================================================

            // SubscriptionSourceDialog（依赖ProfileMarketplaceService, INotificationService）
            services.AddTransient<SubscriptionSourceDialog>();

            // ProfileSelectorDialog（依赖IProfileManager, IPluginAssociationManager, INotificationService, ILogService）
            services.AddTransient<ProfileSelectorDialog>();

            // UninstallConfirmDialog（依赖INotificationService, ILogService）
            services.AddTransient<UninstallConfirmDialog>();

            // ExitRecordPrompt（依赖IPioneerNoteService）
            services.AddTransient<ExitRecordPrompt>();

            // PluginUpdatePromptDialog（依赖IConfigService）
            services.AddTransient<PluginUpdatePromptDialog>();

            // ProfileCreateDialog（依赖IPluginLibrary, IProfileManager）
            services.AddTransient<ProfileCreateDialog>();

            // DialogFactory（工厂模式创建带参数的Dialog）
            services.AddSingleton<IDialogFactory, DialogFactory>();

            // ============================================================
            // Pages（Transient，每次请求创建新实例）
            // ============================================================

            // AvailablePluginsPage（依赖PluginLibrary, INotificationService）
            services.AddTransient<AvailablePluginsPage>();

            // ProfileMarketPage（依赖ProfileMarketplaceService, PluginLibrary, INotificationService）
            services.AddTransient<ProfileMarketPage>();

            return services;
        }
    }
}
