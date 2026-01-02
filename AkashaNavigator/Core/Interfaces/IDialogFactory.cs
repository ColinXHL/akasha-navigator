using System.Collections.Generic;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// 对话框工厂接口，用于创建带参数的对话框
/// </summary>
public interface IDialogFactory
{
    /// <summary>
    /// 创建订阅源管理对话框
    /// </summary>
    SubscriptionSourceDialog CreateSubscriptionSourceDialog();

    /// <summary>
    /// 创建 Profile 选择器对话框
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    ProfileSelectorDialog CreateProfileSelectorDialog(string pluginId);

    /// <summary>
    /// 创建卸载确认对话框
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="pluginName">插件名称（可选）</param>
    UninstallConfirmDialog CreateUninstallConfirmDialog(string pluginId, string? pluginName = null);

    /// <summary>
    /// 创建退出记录提示对话框
    /// </summary>
    /// <param name="url">页面URL</param>
    /// <param name="title">页面标题</param>
    ExitRecordPrompt CreateExitRecordPrompt(string url, string title);

    /// <summary>
    /// 创建插件更新提示对话框
    /// </summary>
    /// <param name="updates">可用更新列表</param>
    PluginUpdatePromptDialog CreatePluginUpdatePromptDialog(List<UpdateCheckResult> updates);

    /// <summary>
    /// 创建 BookmarkPopup（带 ViewModel）
    /// </summary>
    BookmarkPopup CreateBookmarkPopup();

    /// <summary>
    /// 创建 ProfileCreateDialog（带 ViewModel）
    /// </summary>
    ProfileCreateDialog CreateProfileCreateDialog();

    /// <summary>
    /// 创建 ProfileEditDialog（带 ViewModel）
    /// </summary>
    /// <param name="profile">要编辑的 Profile</param>
    ProfileEditDialog CreateProfileEditDialog(Models.Profile.GameProfile profile);

    /// <summary>
    /// 创建 NoteEditDialog（带 ViewModel）
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="prompt">提示文本</param>
    /// <param name="showUrl">是否显示 URL 输入框</param>
    /// <param name="isConfirmDialog">是否为确认对话框（只显示消息和按钮）</param>
    /// <param name="defaultUrl">默认 URL 值</param>
    NoteEditDialog CreateNoteEditDialog(
        string title,
        string defaultValue,
        string prompt = "请输入新名称：",
        bool showUrl = false,
        bool isConfirmDialog = false,
        string? defaultUrl = null);

    /// <summary>
    /// 创建 NoteMoveDialog（带 ViewModel）
    /// </summary>
    /// <param name="folders">目录列表</param>
    /// <param name="currentFolderId">当前所在目录 ID</param>
    NoteMoveDialog CreateNoteMoveDialog(List<NoteFolder> folders, string? currentFolderId);

    /// <summary>
    /// 创建 RecordNoteDialog（带 ViewModel）
    /// </summary>
    /// <param name="url">初始 URL</param>
    /// <param name="title">默认标题</param>
    RecordNoteDialog CreateRecordNoteDialog(string url, string title);
}
}