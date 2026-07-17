using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Update;

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
    private readonly IDownloadSourceSelector _downloadSourceSelector;
    private readonly IUpdateManifestService _updateManifestService;

    public IReadOnlyList<AppUpdateSourceOption> AppUpdateSourceOptions { get; } =
        new[] {
            new AppUpdateSourceOption(AppUpdateSourcePreference.Cnb, "CNB"),
            new AppUpdateSourceOption(AppUpdateSourcePreference.GitHub, "GitHub")
        };

    public IReadOnlyList<PluginDownloadSourceOption> PluginDownloadSourceOptions { get; } =
        new[] {
            new PluginDownloadSourceOption(PluginDownloadSourcePreference.Auto, "自动选择"),
            new PluginDownloadSourceOption(PluginDownloadSourcePreference.GitHub, "GitHub"),
            new PluginDownloadSourceOption(PluginDownloadSourcePreference.Cnb, "CNB")
        };

    [ObservableProperty]
    private bool _isCheckingAppUpdate;

    [ObservableProperty]
    private bool _isMeasuringDownloadSources;

    [ObservableProperty]
    private string _downloadSourceMeasurementStatus =
        "尚未测速；应用更新与插件自动下载共用同一结果";

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

    [ObservableProperty]
    private AppUpdateSourcePreference _appUpdateSourcePreference;

    [ObservableProperty]
    private PluginDownloadSourcePreference _pluginDownloadSourcePreference;

    /// <summary>
    /// 是否启用调试日志（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableDebugLog;

    public AdvancedSettingsPageViewModel(
        IAppUpdateService appUpdateService,
        INotificationService notificationService,
        ILogService logService,
        IDownloadSourceSelector downloadSourceSelector,
        IUpdateManifestService updateManifestService)
    {
        _appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _downloadSourceSelector =
            downloadSourceSelector ?? throw new ArgumentNullException(nameof(downloadSourceSelector));
        _updateManifestService =
            updateManifestService ?? throw new ArgumentNullException(nameof(updateManifestService));

        IsPrereleaseToggleEnabled = true;
    }

    /// <summary>
    /// 从配置对象加载设置
    /// </summary>
    public void LoadSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnablePrereleaseUpdate = config.EnablePrereleaseUpdate;
        AppUpdateSourcePreference = config.AppUpdateSourcePreference;
        PluginDownloadSourcePreference = config.PluginDownloadSourcePreference;
        EnableDebugLog = config.EnableDebugLog;
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.EnablePluginUpdateNotification = EnablePluginUpdateNotification;
        config.EnablePrereleaseUpdate = EnablePrereleaseUpdate;
        config.AppUpdateSourcePreference = AppUpdateSourcePreference;
        config.PluginDownloadSourcePreference = PluginDownloadSourcePreference;
        config.EnableDebugLog = EnableDebugLog;
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnablePrereleaseUpdate = config.EnablePrereleaseUpdate;
        AppUpdateSourcePreference = config.AppUpdateSourcePreference;
        PluginDownloadSourcePreference = config.PluginDownloadSourcePreference;
        EnableDebugLog = config.EnableDebugLog;
    }

    [RelayCommand]
    private async Task MeasureDownloadSourcesAsync()
    {
        if (IsMeasuringDownloadSources)
        {
            return;
        }

        IsMeasuringDownloadSources = true;
        DownloadSourceMeasurementStatus = "正在读取 CNB 与 GitHub 的实际安装包数据…";
        try
        {
            var manifestResult = await _updateManifestService.RefreshAsync();
            if (manifestResult.IsFailure || manifestResult.Value == null)
            {
                DownloadSourceMeasurementStatus = "测速失败：无法获取最新版本信息";
                _notificationService.Error("测速失败，无法获取最新版本信息", "下载源测速");
                return;
            }

            var package = CreateAppUpdateProbePackage(
                manifestResult.Value,
                EnablePrereleaseUpdate);
            if (package == null)
            {
                DownloadSourceMeasurementStatus = "测速失败：更新清单中没有可用版本";
                _notificationService.Error("更新清单中没有可用于测速的版本", "下载源测速");
                return;
            }

            var measurementResult = await _downloadSourceSelector.MeasureSourcesAsync(
                package,
                forceRefresh: true);
            if (measurementResult.IsFailure || measurementResult.Value == null)
            {
                DownloadSourceMeasurementStatus =
                    $"测速失败：{measurementResult.Error?.Message ?? "未知错误"}";
                _notificationService.Error("下载源测速失败，请稍后重试", "下载源测速");
                return;
            }

            var recommended = measurementResult.Value.First(measurement => measurement.IsSuccess);
            ApplyRecommendedSource(recommended.Source.Id);
            DownloadSourceMeasurementStatus = FormatMeasurements(measurementResult.Value);
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(AdvancedSettingsPageViewModel), ex, "下载源测速时发生异常");
            DownloadSourceMeasurementStatus = "测速失败：发生异常";
            _notificationService.Error("下载源测速时出现异常", "下载源测速");
        }
        finally
        {
            IsMeasuringDownloadSources = false;
        }
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
            var sourceId = ResolveAppUpdateSourceId(updateInfo.IsPrerelease);
            var sourceText =
                AppUpdateSourcePreference == AppUpdateSourcePreference.GitHub ? "GitHub" : "CNB";
            var notes = string.IsNullOrWhiteSpace(updateInfo.Notes) ? "暂无更新说明" : updateInfo.Notes;
            var message =
                $"发现新版本 v{updateInfo.TargetVersion} ({channelText})\n\n{notes}\n\n下载源：{sourceText}\n是否立即更新？";
            var confirmed = await _notificationService.ConfirmAsync(message, "版本更新");
            if (!confirmed)
            {
                return;
            }

            var startResult = _appUpdateService.StartUpdater(sourceId);
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

    private string ResolveAppUpdateSourceId(bool isPrerelease)
    {
        if (AppUpdateSourcePreference == AppUpdateSourcePreference.GitHub)
        {
            return "github";
        }

        return isPrerelease ? "cnb-alpha" : "cnb";
    }

    private static PluginPackageInfo? CreateAppUpdateProbePackage(
        UpdateManifest manifest,
        bool includePrerelease)
    {
        var channel = includePrerelease && !string.IsNullOrWhiteSpace(manifest.Alpha?.Version)
            ? manifest.Alpha
            : manifest.Stable ?? manifest.Alpha;
        if (string.IsNullOrWhiteSpace(channel?.Version))
        {
            return null;
        }

        var version = channel.Version!;
        var isPrerelease = ReferenceEquals(channel, manifest.Alpha);
        var cnbRepository = isPrerelease
            ? "akasha-navigator-alpha"
            : "akasha-navigator";
        var installerName = $"AkashaNavigator.Install.{version}.exe";

        return new PluginPackageInfo {
            FileName = installerName,
            Size = 0,
            Sources = {
                new DownloadSourceInfo {
                    Id = "cnb",
                    Url =
                        $"https://cnb.cool/AkashaNavigator/{cnbRepository}/-/releases/download/v{version}/{installerName}"
                },
                new DownloadSourceInfo {
                    Id = "github",
                    Url =
                        $"https://github.com/ColinXHL/akasha-navigator/releases/download/v{version}/{installerName}"
                }
            }
        };
    }

    private static string FormatMeasurements(
        IReadOnlyList<DownloadSourceMeasurement> measurements)
    {
        var details = measurements.Select(measurement =>
        {
            var sourceName = GetSourceDisplayName(measurement.Source.Id);
            if (!measurement.IsSuccess)
            {
                return $"{sourceName}：测速失败";
            }

            var speed = measurement.BytesPerSecond >= 1024 * 1024
                ? $"{measurement.BytesPerSecond / 1024 / 1024:F1} MiB/s"
                : $"{measurement.BytesPerSecond / 1024:F0} KiB/s";
            return $"{sourceName}：{speed} · 首字节 {measurement.TimeToFirstByte.TotalMilliseconds:F0} ms";
        });
        var recommended = measurements.First(measurement => measurement.IsSuccess);
        return
            $"{string.Join(Environment.NewLine, details)}{Environment.NewLine}" +
            $"推荐：{GetSourceDisplayName(recommended.Source.Id)} · 已应用到版本更新和插件下载";
    }

    private void ApplyRecommendedSource(string sourceId)
    {
        if (string.Equals(sourceId, "github", StringComparison.OrdinalIgnoreCase))
        {
            AppUpdateSourcePreference = AppUpdateSourcePreference.GitHub;
            PluginDownloadSourcePreference = PluginDownloadSourcePreference.GitHub;
            return;
        }

        if (string.Equals(sourceId, "cnb", StringComparison.OrdinalIgnoreCase))
        {
            AppUpdateSourcePreference = AppUpdateSourcePreference.Cnb;
            PluginDownloadSourcePreference = PluginDownloadSourcePreference.Cnb;
        }
    }

    private static string GetSourceDisplayName(string sourceId)
    {
        return string.Equals(sourceId, "github", StringComparison.OrdinalIgnoreCase)
            ? "GitHub"
            : string.Equals(sourceId, "cnb", StringComparison.OrdinalIgnoreCase)
                ? "CNB"
                : sourceId;
    }
}

public sealed record AppUpdateSourceOption(
    AppUpdateSourcePreference Value,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record PluginDownloadSourceOption(
    PluginDownloadSourcePreference Value,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}
