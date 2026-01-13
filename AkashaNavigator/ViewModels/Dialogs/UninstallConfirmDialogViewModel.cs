using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.ViewModels.Dialogs
{
/// <summary>
/// 插件卸载确认对话框 ViewModel
/// 显示关联的 Profile 列表并确认卸载
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class UninstallConfirmDialogViewModel : ObservableObject
{
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly ILogService _logService;
    private readonly IEventBus _eventBus;

    /// <summary>
    /// 引用此插件的 Profile 名称列表
    /// </summary>
    public ObservableCollection<string> ReferencingProfiles { get; }

    /// <summary>
    /// 插件 ID
    /// </summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>
    /// 插件名称（用于显示）
    /// </summary>
    [ObservableProperty]
    private string _pluginName = string.Empty;

    /// <summary>
    /// 确认提示文本
    /// </summary>
    [ObservableProperty]
    private string _confirmPromptText = "确定要卸载此插件吗？";

    /// <summary>
    /// 是否有 Profile 引用此插件
    /// </summary>
    [ObservableProperty]
    private bool _hasReferencingProfiles;

    /// <summary>
    /// 卸载是否成功
    /// </summary>
    public bool UninstallSucceeded { get; private set; }

    /// <summary>
    /// 错误信息（如果卸载失败）
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 请求关闭对话框事件（参数：是否确认卸载）
    /// </summary>
    public event EventHandler<bool>? RequestClose;

    /// <summary>
    /// 创建 UninstallConfirmDialogViewModel
    /// </summary>
    public UninstallConfirmDialogViewModel(IPluginAssociationManager pluginAssociationManager,
                                           IPluginLibrary pluginLibrary, ILogService logService, IEventBus eventBus)
    {
        _pluginAssociationManager =
            pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        ReferencingProfiles = new ObservableCollection<string>();
    }

    /// <summary>
    /// 初始化 ViewModel（传入参数）
    /// </summary>
    public void Initialize(string pluginId, string? pluginName = null)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        PluginName = pluginName ?? pluginId;
        ConfirmPromptText = $"确定要卸载 \"{PluginName}\" 吗？";

        // 获取引用此插件的 Profile 列表
        var referencingProfiles = _pluginAssociationManager.GetProfilesUsingPlugin(pluginId);
        ReferencingProfiles.Clear();
        foreach (var profile in referencingProfiles)
        {
            ReferencingProfiles.Add(profile);
        }
        HasReferencingProfiles = referencingProfiles.Count > 0;
    }

    /// <summary>
    /// 取消命令（自动生成 CancelCommand）
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    /// <summary>
    /// 确认卸载命令（自动生成 ConfirmCommand）
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        // 执行卸载
        UninstallSucceeded = PerformUninstall();
        RequestClose?.Invoke(this, UninstallSucceeded);
    }

    /// <summary>
    /// 执行卸载操作
    /// </summary>
    private bool PerformUninstall()
    {
        // 1. 如果有关联的 Profile，先清理关联关系
        if (HasReferencingProfiles && ReferencingProfiles.Count > 0)
        {
            var removedCount = _pluginAssociationManager.RemovePluginFromAllProfiles(PluginId);
            _logService.Info(nameof(UninstallConfirmDialogViewModel),
                             "已从 {RemovedCount} 个 Profile 中移除插件 {PluginId} 的引用", removedCount, PluginId);
        }

        // 2. 执行卸载（强制模式，因为关联已清理）
        var uninstallResult = _pluginLibrary.UninstallPlugin(PluginId, force: true);

        if (uninstallResult.IsSuccess)
        {
            _logService.Info(nameof(UninstallConfirmDialogViewModel), "插件 {PluginId} 卸载成功", PluginId);
            ErrorMessage = null;

            // 3. 通知其他页面刷新插件列表
            _eventBus.Publish(new PluginListChangedEvent());

            return true;
        }
        else
        {
            ErrorMessage = uninstallResult.Error?.Message;
            _logService.Error(nameof(UninstallConfirmDialogViewModel), "插件 {PluginId} 卸载失败: {ErrorMessage}",
                              PluginId, ErrorMessage ?? "未知错误");
            return false;
        }
    }
}
}
