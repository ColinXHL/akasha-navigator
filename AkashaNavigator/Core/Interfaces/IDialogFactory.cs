using System.Collections.Generic;
using AkashaNavigator.Models.Common;
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
}
}
