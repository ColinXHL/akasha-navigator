using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Services
{
/// <summary>
/// 对话框工厂实现，负责创建带参数的对话框实例
/// </summary>
public class DialogFactory : IDialogFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DialogFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 创建订阅源管理对话框
    /// </summary>
    public SubscriptionSourceDialog CreateSubscriptionSourceDialog()
    {
        var profileMarketplaceService = _serviceProvider.GetRequiredService<ProfileMarketplaceService>();
        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();

        return new SubscriptionSourceDialog(profileMarketplaceService, notificationService);
    }

    /// <summary>
    /// 创建 Profile 选择器对话框
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public ProfileSelectorDialog CreateProfileSelectorDialog(string pluginId)
    {
        var profileManager = _serviceProvider.GetRequiredService<IProfileManager>();
        var pluginAssociationManager = _serviceProvider.GetRequiredService<IPluginAssociationManager>();
        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        var logService = _serviceProvider.GetRequiredService<ILogService>();

        return new ProfileSelectorDialog(profileManager, pluginAssociationManager, notificationService, logService, pluginId);
    }

    /// <summary>
    /// 创建卸载确认对话框
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="pluginName">插件名称（可选）</param>
    public UninstallConfirmDialog CreateUninstallConfirmDialog(string pluginId, string? pluginName = null)
    {
        var pluginAssociationManager = _serviceProvider.GetRequiredService<IPluginAssociationManager>();
        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        var logService = _serviceProvider.GetRequiredService<ILogService>();
        var pluginLibrary = _serviceProvider.GetRequiredService<IPluginLibrary>();

        return new UninstallConfirmDialog(pluginAssociationManager, notificationService, logService, pluginLibrary, pluginId, pluginName);
    }

    /// <summary>
    /// 创建退出记录提示对话框
    /// </summary>
    /// <param name="url">页面URL</param>
    /// <param name="title">页面标题</param>
    public ExitRecordPrompt CreateExitRecordPrompt(string url, string title)
    {
        var pioneerNoteService = _serviceProvider.GetRequiredService<IPioneerNoteService>();

        return new ExitRecordPrompt(pioneerNoteService, url, title);
    }

    /// <summary>
    /// 创建插件更新提示对话框
    /// </summary>
    /// <param name="updates">可用更新列表</param>
    public PluginUpdatePromptDialog CreatePluginUpdatePromptDialog(List<UpdateCheckResult> updates)
    {
        var configService = _serviceProvider.GetRequiredService<IConfigService>();

        return new PluginUpdatePromptDialog(configService, updates);
    }
}
}
