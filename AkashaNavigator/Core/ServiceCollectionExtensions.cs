using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Pages;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.ViewModels.Pages.Settings;
using AkashaNavigator.ViewModels.Windows;

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

        // EventBus（无依赖，用于组件间解耦通信）
        services.AddSingleton<IEventBus, EventBus>();

        // LogService（无依赖）
        services.AddSingleton<ILogService, LogService>();

        // CursorDetectionService（无依赖）
        services.AddSingleton<ICursorDetectionService, CursorDetectionService>();

        // PluginRegistry（依赖LogService）
        services.AddSingleton<IPluginRegistry, PluginRegistry>();

        // ProfileRegistry（依赖LogService）
        services.AddSingleton<IProfileRegistry, ProfileRegistry>();

        // PluginLibrary（无依赖）
        // 只注册接口，避免重复注册导致多个实例
        services.AddSingleton<IPluginLibrary, PluginLibrary>();

        // HotkeyService（无依赖，使用Win32钩子）
        services.AddSingleton<HotkeyService>();

        // ============================================================
        // Level 1: 依赖 LogService
        // ============================================================

        // ConfigService（依赖LogService）
        services.AddSingleton<IConfigService, ConfigService>();

        // NotificationService（依赖LogService + Func<IDialogFactory>延迟解析）
        services.AddSingleton<INotificationService>(
            sp =>
            {
                var logService = sp.GetRequiredService<ILogService>();
                // 使用 Func 延迟解析 IDialogFactory，避免循环依赖
                Func<IDialogFactory> dialogFactoryProvider = () => sp.GetRequiredService<IDialogFactory>();
                return new NotificationService(logService, dialogFactoryProvider);
            });

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

        // ProfileManager（依赖ConfigService, LogService, PluginHost, PluginAssociationManager, SubscriptionManager,
        // PluginLibrary, ProfileRegistry）
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

        // OverlayManager（使用现有单例实例，私有构造函数）
        services.AddSingleton<IOverlayManager>(sp => OverlayManager.Instance);

        // ============================================================
        // ViewModels（Pages）- 必须在 PluginCenterViewModel 之前注册
        // ============================================================

        // MyProfilesPageViewModel（依赖 ProfileManager + PluginAssociationManager + PluginLibrary + EventBus）
        services.AddTransient<MyProfilesPageViewModel>();

        // InstalledPluginsPageViewModel（依赖 PluginLibrary + PluginAssociationManager + ProfileManager +
        // NotificationService）
        services.AddTransient<InstalledPluginsPageViewModel>();

        // AvailablePluginsPageViewModel（依赖 PluginLibrary + NotificationService）
        services.AddTransient<AvailablePluginsPageViewModel>();

        // ProfileMarketPageViewModel（依赖 ProfileMarketplaceService + PluginLibrary + ProfileManager +
        // NotificationService）
        services.AddTransient<ProfileMarketPageViewModel>();

        // ============================================================
        // ViewModels（Pages - Settings）
        // 必须在 SettingsViewModel 之前注册（依赖链：SettingsViewModel → PageViewModels）
        // ============================================================

        // GeneralSettingsPageViewModel（依赖 ConfigService + ProfileManager）
        services.AddTransient<GeneralSettingsPageViewModel>();

        // WindowSettingsPageViewModel（无依赖）
        services.AddTransient<WindowSettingsPageViewModel>();

        // HotkeySettingsPageViewModel（无依赖，内部创建 HotkeyConflictDetector）
        services.AddTransient<HotkeySettingsPageViewModel>();

        // AdvancedSettingsPageViewModel（无依赖）
        services.AddTransient<AdvancedSettingsPageViewModel>();

        // ============================================================
        // ViewModels（Windows）
        // ============================================================

        // PlayerViewModel（依赖 ProfileManager + EventBus）
        services.AddTransient<PlayerViewModel>();

        // ControlBarViewModel（依赖 EventBus）
        services.AddTransient<ControlBarViewModel>();

        // HistoryWindowViewModel（依赖 DataService）
        services.AddTransient<HistoryWindowViewModel>();

        // SettingsViewModel（依赖 ConfigService + ProfileManager + EventBus + 4 个 PageViewModels）
        // 依赖链：SettingsViewModel → (GeneralSettingsPageViewModel, WindowSettingsPageViewModel,
        //         HotkeySettingsPageViewModel, AdvancedSettingsPageViewModel)
        services.AddTransient<SettingsViewModel>();

        // PluginCenterViewModel（依赖 4 个 PageViewModel）
        // 依赖链：PluginCenterViewModel → (MyProfilesPageViewModel, InstalledPluginsPageViewModel,
        //         AvailablePluginsPageViewModel, ProfileMarketPageViewModel)
        services.AddTransient<PluginCenterViewModel>();

        // PioneerNoteViewModel（依赖 IPioneerNoteService）
        services.AddTransient<PioneerNoteViewModel>();

        // ============================================================
        // Pages（Transient，每次请求创建新实例）
        // 必须在 PluginCenterWindow 之前注册
        // ============================================================

        // MyProfilesPage（依赖 MyProfilesPageViewModel + IDialogFactory + IProfileManager + IPluginLibrary +
        //                IPluginHost + IPluginAssociationManager + INotificationService）
        services.AddTransient<MyProfilesPage>();

        // InstalledPluginsPage（依赖 InstalledPluginsPageViewModel + IPluginLibrary + IDialogFactory）
        services.AddTransient<InstalledPluginsPage>();

        // AvailablePluginsPage（依赖 AvailablePluginsPageViewModel + IDialogFactory）
        services.AddTransient<AvailablePluginsPage>();

        // MarketplaceProfileDetailDialogViewModel 工厂方法（用于 ProfileMarketPage 延迟创建）
        services.AddSingleton<Func<MarketplaceProfileDetailDialogViewModel>>(
            sp => () => sp.GetRequiredService<MarketplaceProfileDetailDialogViewModel>());

        // ProfileMarketPage（依赖 ProfileMarketPageViewModel + IDialogFactory +
        //                   Func<MarketplaceProfileDetailDialogViewModel>）
        services.AddTransient<ProfileMarketPage>();

        // ============================================================
        // Pages（Settings）
        // ============================================================

        // GeneralSettingsPage（DataContext 由 SettingsWindow 设置）
        services.AddTransient<GeneralSettingsPage>();

        // WindowSettingsPage（DataContext 由 SettingsWindow 设置）
        services.AddTransient<WindowSettingsPage>();

        // HotkeySettingsPage（DataContext 由 SettingsWindow 设置）
        services.AddTransient<HotkeySettingsPage>();

        // AdvancedSettingsPage（DataContext 由 SettingsWindow 设置）
        services.AddTransient<AdvancedSettingsPage>();

        // ============================================================
        // 窗口（Transient，每次请求创建新实例）
        // ============================================================

        // PlayerWindow（依赖所有服务 + IDialogFactory + PioneerNoteWindow 工厂）
        services.AddSingleton<PlayerWindow>();

        // ControlBarWindow（依赖 ControlBarViewModel + PlayerWindow）
        services.AddSingleton<ControlBarWindow>();

        // PioneerNoteWindow 工厂方法（用于 PlayerWindow 延迟创建）
        services.AddSingleton<Func<PioneerNoteWindow>>(sp => () => sp.GetRequiredService<PioneerNoteWindow>());

        // SettingsWindow（依赖 SettingsViewModel + NotificationService）
        // 依赖链：SettingsWindow → SettingsViewModel → (ConfigService, ProfileManager, EventBus)
        services.AddTransient<SettingsWindow>();

        // PluginCenterWindow（依赖 PluginCenterViewModel + 4 个 Page）
        // 依赖链：PluginCenterWindow → (PluginCenterViewModel, MyProfilesPage, InstalledPluginsPage,
        //         AvailablePluginsPage, ProfileMarketPage)
        services.AddTransient<PluginCenterWindow>();

        // PluginSettingsWindow 工厂方法（用于创建带参数的窗口）
        services.AddSingleton < Func < string, string, string, string, string ?,
            PluginSettingsWindow >> (sp =>
                                     {
                                         return (pluginId, pluginName, pluginDirectory, configDirectory, profileId) =>
                                         {
                                             var profileManager = sp.GetRequiredService<IProfileManager>();
                                             var logService = sp.GetRequiredService<ILogService>();
                                             var pluginHost = sp.GetRequiredService<IPluginHost>();
                                             var notificationService = sp.GetRequiredService<INotificationService>();
                                             var overlayManager = sp.GetRequiredService<IOverlayManager>();
                                             return new PluginSettingsWindow(profileManager, logService, pluginHost,
                                                                             notificationService, overlayManager,
                                                                             pluginId, pluginName, pluginDirectory,
                                                                             configDirectory, profileId);
                                         };
                                     });

        // HistoryWindow（依赖 HistoryWindowViewModel + IDialogFactory）
        // 依赖链：HistoryWindow → (HistoryWindowViewModel, IDialogFactory)
        services.AddTransient<HistoryWindow>();

        // BookmarkPopup（依赖 BookmarkPopupViewModel + IDialogFactory）
        // 注意：BookmarkPopup 通过 DialogFactory.CreateBookmarkPopup() 创建，不直接从 DI 获取
        services.AddTransient<BookmarkPopupViewModel>();

        // ProfileCreateDialog（依赖ProfileCreateDialogViewModel）
        services.AddTransient<ProfileCreateDialogViewModel>();
        services.AddTransient<ProfileCreateDialog>();

        // ProfileEditDialog（依赖ProfileEditDialogViewModel）
        services.AddTransient<ProfileEditDialogViewModel>();

        // PluginUpdatePromptDialog（依赖PluginUpdatePromptDialogViewModel）
        services.AddTransient<PluginUpdatePromptDialogViewModel>();

        // RecordNoteDialog（依赖RecordNoteDialogViewModel）
        services.AddTransient<RecordNoteDialogViewModel>();

        // PluginSelectorDialog（依赖PluginSelectorDialogViewModel）
        services.AddTransient<PluginSelectorDialogViewModel>();

        // MarketplaceProfileDetailDialog（依赖MarketplaceProfileDetailDialogViewModel）
        services.AddTransient<MarketplaceProfileDetailDialogViewModel>();

        // WelcomeDialog（依赖WelcomeDialogViewModel）
        services.AddTransient<WelcomeDialogViewModel>();
        services.AddTransient<WelcomeDialog>();

        // SubscriptionSourceDialog（依赖SubscriptionSourceDialogViewModel）
        services.AddTransient<SubscriptionSourceDialogViewModel>();

        // ExitRecordPrompt（依赖ExitRecordPromptViewModel）
        services.AddTransient<ExitRecordPromptViewModel>();

        // ProfileSelectorDialog（依赖ProfileSelectorDialogViewModel）
        services.AddTransient<ProfileSelectorDialogViewModel>();

        // UninstallConfirmDialog（依赖UninstallConfirmDialogViewModel）
        services.AddTransient<UninstallConfirmDialogViewModel>();

        // RecordNoteDialog 工厂方法（委托到 IDialogFactory）
        services.AddSingleton<Func<string, string, RecordNoteDialog>>(
            sp =>
            {
                return (url, title) =>
                {
                    var dialogFactory = sp.GetRequiredService<IDialogFactory>();
                    return dialogFactory.CreateRecordNoteDialog(url, title);
                };
            });

        // PioneerNoteWindow（依赖 PioneerNoteViewModel + IDialogFactory + IPioneerNoteService）
        // 依赖链：PioneerNoteWindow → (PioneerNoteViewModel, IDialogFactory, IPioneerNoteService)
        services.AddTransient<PioneerNoteWindow>();

        // ============================================================
        // Dialogs（Transient，每次请求创建新实例）
        // ============================================================

        // SubscriptionSourceDialog 已迁移到 MVVM，通过 DialogFactory 创建

        // ProfileSelectorDialog 已迁移到 MVVM，通过 DialogFactory 创建

        // UninstallConfirmDialog 已迁移到 MVVM，通过 DialogFactory 创建

        // ExitRecordPrompt 已迁移到 MVVM，通过 DialogFactory 创建

        // PluginUpdatePromptDialog 已迁移到 MVVM，通过 DialogFactory 创建

        // DialogFactory（工厂模式创建带参数的Dialog）
        services.AddSingleton<IDialogFactory, DialogFactory>();

        return services;
    }
}
}
