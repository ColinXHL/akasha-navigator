using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.ViewModels.Dialogs;

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
        /// 创建订阅源管理对话框（带 ViewModel）
        /// </summary>
        public SubscriptionSourceDialog CreateSubscriptionSourceDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<SubscriptionSourceDialogViewModel>();
            return new SubscriptionSourceDialog(viewModel);
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
        /// 创建卸载确认对话框（带 ViewModel）
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="pluginName">插件名称（可选）</param>
        public UninstallConfirmDialog CreateUninstallConfirmDialog(string pluginId, string? pluginName = null)
        {
            var viewModel = _serviceProvider.GetRequiredService<UninstallConfirmDialogViewModel>();
            return new UninstallConfirmDialog(viewModel, pluginId, pluginName);
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
        /// 创建插件更新提示对话框（带 ViewModel）
        /// </summary>
        /// <param name="updates">可用更新列表</param>
        public PluginUpdatePromptDialog CreatePluginUpdatePromptDialog(List<UpdateCheckResult> updates)
        {
            var viewModel = _serviceProvider.GetRequiredService<PluginUpdatePromptDialogViewModel>();
            viewModel.Initialize(updates);
            return new PluginUpdatePromptDialog(viewModel);
        }

        /// <summary>
        /// 创建 BookmarkPopup（带 ViewModel）
        /// </summary>
        public BookmarkPopup CreateBookmarkPopup()
        {
            var viewModel = _serviceProvider.GetRequiredService<BookmarkPopupViewModel>();
            return new BookmarkPopup(viewModel);
        }

        /// <summary>
        /// 创建 ProfileCreateDialog（带 ViewModel）
        /// </summary>
        public ProfileCreateDialog CreateProfileCreateDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<ProfileCreateDialogViewModel>();
            return new ProfileCreateDialog(viewModel);
        }

        /// <summary>
        /// 创建 ProfileEditDialog（带 ViewModel）
        /// </summary>
        /// <param name="profile">要编辑的 Profile</param>
        public ProfileEditDialog CreateProfileEditDialog(Models.Profile.GameProfile profile)
        {
            var viewModel = _serviceProvider.GetRequiredService<ProfileEditDialogViewModel>();
            viewModel.Initialize(profile);
            return new ProfileEditDialog(viewModel);
        }

        /// <summary>
        /// 创建 NoteEditDialog（带 ViewModel）
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="prompt">提示文本</param>
        /// <param name="showUrl">是否显示 URL 输入框</param>
        /// <param name="isConfirmDialog">是否为确认对话框（只显示消息和按钮）</param>
        /// <param name="defaultUrl">默认 URL 值</param>
        public NoteEditDialog CreateNoteEditDialog(
            string title,
            string defaultValue,
            string prompt = "请输入新名称：",
            bool showUrl = false,
            bool isConfirmDialog = false,
            string? defaultUrl = null)
        {
            var viewModel = new NoteEditDialogViewModel(title, defaultValue, prompt, showUrl, isConfirmDialog, defaultUrl);
            return new NoteEditDialog(viewModel);
        }

        /// <summary>
        /// 创建 NoteMoveDialog（带 ViewModel）
        /// </summary>
        /// <param name="folders">目录列表</param>
        /// <param name="currentFolderId">当前所在目录 ID</param>
        public NoteMoveDialog CreateNoteMoveDialog(List<NoteFolder> folders, string? currentFolderId)
        {
            var viewModel = new NoteMoveDialogViewModel(folders, currentFolderId);
            return new NoteMoveDialog(viewModel);
        }

        /// <summary>
        /// 创建 RecordNoteDialog（带 ViewModel）
        /// </summary>
        /// <param name="url">初始 URL</param>
        /// <param name="title">默认标题</param>
        public RecordNoteDialog CreateRecordNoteDialog(string url, string title)
        {
            var viewModel = _serviceProvider.GetRequiredService<RecordNoteDialogViewModel>();
            viewModel.Initialize(url, title);
            return new RecordNoteDialog(viewModel, this);
        }

        /// <summary>
        /// 创建 PluginSelectorDialog（带 ViewModel）
        /// </summary>
        /// <param name="availablePlugins">可用插件列表</param>
        /// <param name="profileId">Profile ID</param>
        public PluginSelectorDialog CreatePluginSelectorDialog(List<Models.Plugin.InstalledPluginInfo> availablePlugins, string profileId)
        {
            var viewModel = _serviceProvider.GetRequiredService<PluginSelectorDialogViewModel>();
            var dialog = new PluginSelectorDialog(viewModel);
            dialog.InitializePlugins(availablePlugins, profileId);
            return dialog;
        }

        /// <summary>
        /// 创建 ConfirmDialog（带 ViewModel）
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="confirmText">确定按钮文本</param>
        /// <param name="cancelText">取消按钮文本</param>
        public ConfirmDialog CreateConfirmDialog(string message, string? title = null, string confirmText = "确定", string cancelText = "取消")
        {
            var viewModel = new ConfirmDialogViewModel(message, title, confirmText, cancelText);
            return new ConfirmDialog(viewModel);
        }

        /// <summary>
        /// 创建 WelcomeDialog（带 ViewModel）
        /// </summary>
        public WelcomeDialog CreateWelcomeDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<WelcomeDialogViewModel>();
            return new WelcomeDialog(viewModel);
        }

        /// <summary>
        /// 创建 PluginUninstallDialog（带 ViewModel）
        /// </summary>
        /// <param name="profileName">Profile 名称（用于显示）</param>
        /// <param name="plugins">唯一插件列表</param>
        public PluginUninstallDialog CreatePluginUninstallDialog(string profileName, List<Models.Plugin.PluginUninstallItem> plugins)
        {
            var viewModel = new PluginUninstallDialogViewModel(profileName, plugins);
            return new PluginUninstallDialog(viewModel);
        }
    }
}
