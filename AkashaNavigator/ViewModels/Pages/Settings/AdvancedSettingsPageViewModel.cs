using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 高级设置页面 ViewModel
/// 包含：版本更新、插件更新提示、调试日志
/// </summary>
public partial class AdvancedSettingsPageViewModel : ObservableObject
{
    private readonly IAppUpdateService _appUpdateService;
    private readonly INotificationService _notificationService;
    private readonly ILogService _logService;

    [ObservableProperty]
    private bool _isCheckingAppUpdate;

    [ObservableProperty]
    private bool _isPrereleaseToggleEnabled;

    /// <summary>
    /// 是否启用插件更新通知（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enablePluginUpdateNotification;

    /// <summary>
    /// 是否包含测试版更新（当前版本固定开启）
    /// </summary>
    [ObservableProperty]
    private bool _enablePrereleaseUpdate;

    /// <summary>
    /// 是否启用调试日志（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableDebugLog;

    public AdvancedSettingsPageViewModel(IAppUpdateService appUpdateService, INotificationService notificationService,
                                         ILogService logService)
    {
        _appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        IsPrereleaseToggleEnabled = true;
    }

    /// <summary>
    /// 从配置对象加载设置
    /// </summary>
    public void LoadSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnablePrereleaseUpdate = config.EnablePrereleaseUpdate;
        EnableDebugLog = config.EnableDebugLog;
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.EnablePluginUpdateNotification = EnablePluginUpdateNotification;
        config.EnablePrereleaseUpdate = EnablePrereleaseUpdate;
        config.EnableDebugLog = EnableDebugLog;
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnablePrereleaseUpdate = config.EnablePrereleaseUpdate;
        EnableDebugLog = config.EnableDebugLog;
    }

    [RelayCommand]
    private async Task CheckAppUpdateAsync()
    {
        if (IsCheckingAppUpdate)
        {
            return;
        }

        IsCheckingAppUpdate = true;
        try
        {
            var checkResult = await _appUpdateService.CheckForUpdateAsync(EnablePrereleaseUpdate);
            if (checkResult.IsFailure || checkResult.Value == null)
            {
                _logService.Warn(nameof(AdvancedSettingsPageViewModel), "检查应用更新失败: {Error}",
                                 checkResult.Error?.Message ?? "未知错误");
                _notificationService.Error("检查更新失败，请稍后重试", "版本更新");
                return;
            }

            var updateInfo = checkResult.Value;
            if (!updateInfo.HasUpdate)
            {
                _notificationService.Info("当前已是最新版本", "版本更新");
                return;
            }

            var channelText = updateInfo.IsPrerelease ? "测试版" : "稳定版";
            var notes = string.IsNullOrWhiteSpace(updateInfo.Notes) ? "暂无更新说明" : updateInfo.Notes;
            var message =
                $"发现新版本 v{updateInfo.TargetVersion} ({channelText})\n\n{notes}\n\n是否立即更新？";
            var confirmed = await _notificationService.ConfirmAsync(message, "版本更新");
            if (!confirmed)
            {
                return;
            }

            var startResult = _appUpdateService.StartUpdater(updateInfo.SourceId);
            if (startResult.IsFailure)
            {
                _notificationService.Error("无法启动更新程序，请确认安装目录内存在 AkashaNavigator.update.exe", "版本更新");
            }
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(AdvancedSettingsPageViewModel), ex, "检查应用更新时发生异常");
            _notificationService.Error("检查更新时出现异常", "版本更新");
        }
        finally
        {
            IsCheckingAppUpdate = false;
        }
    }
}
