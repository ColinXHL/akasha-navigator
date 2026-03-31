using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Services;

public class AppUpdateService : IAppUpdateService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogService _logService;

    public AppUpdateService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public async Task<Result<AppUpdateCheckResult>> CheckForUpdateAsync(bool includePrerelease)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var response = await HttpClient.GetAsync(AppConstants.NoticeJsonUrl);
            if (!response.IsSuccessStatusCode)
            {
                return Result<AppUpdateCheckResult>.Failure(
                    Error.Network("UPDATE_NOTICE_FETCH_FAILED",
                                  $"获取更新信息失败，HTTP {(int)response.StatusCode}",
                                  url: AppConstants.NoticeJsonUrl));
            }

            var json = await response.Content.ReadAsStringAsync();
            var notice = JsonSerializer.Deserialize<UpdateNotice>(json, JsonOptions);
            if (notice == null)
            {
                return Result<AppUpdateCheckResult>.Failure(
                    Error.Serialization("UPDATE_NOTICE_PARSE_FAILED", "更新信息为空或格式无效",
                                        filePath: AppConstants.NoticeJsonUrl));
            }

            var candidate = SelectCandidateVersion(notice, includePrerelease);
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Version))
            {
                return Result<AppUpdateCheckResult>.Failure(
                    Error.Validation("UPDATE_NOTICE_NO_VERSION", "未找到可用版本信息"));
            }

            var compare = CompareSemVer(candidate.Version, currentVersion);
            if (compare <= 0)
            {
                return Result<AppUpdateCheckResult>.Success(AppUpdateCheckResult.NoUpdate(currentVersion));
            }

            var sourceId = ResolveSourceId(candidate);
            var notes = candidate.Notes ?? string.Empty;
            var updateResult = AppUpdateCheckResult.WithUpdate(currentVersion, candidate.Version, notes,
                                                                candidate.IsPrerelease, sourceId);
            return Result<AppUpdateCheckResult>.Success(updateResult);
        }
        catch (HttpRequestException ex)
        {
            return Result<AppUpdateCheckResult>.Failure(
                Error.Network("UPDATE_NOTICE_HTTP_EXCEPTION", ex.Message, ex, AppConstants.NoticeJsonUrl));
        }
        catch (TaskCanceledException ex)
        {
            return Result<AppUpdateCheckResult>.Failure(
                Error.Network("UPDATE_NOTICE_TIMEOUT", "请求更新信息超时", ex, AppConstants.NoticeJsonUrl));
        }
        catch (JsonException ex)
        {
            return Result<AppUpdateCheckResult>.Failure(
                Error.Serialization("UPDATE_NOTICE_JSON_EXCEPTION", "更新信息 JSON 解析失败", ex,
                                    AppConstants.NoticeJsonUrl));
        }
        catch (Exception ex)
        {
            return Result<AppUpdateCheckResult>.Failure(Error.Unknown("UPDATE_CHECK_UNKNOWN", ex.Message, ex));
        }
    }

    public Result StartUpdater(string sourceId = "cnb")
    {
        var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.UpdaterFileName);
        if (!File.Exists(updaterPath))
        {
            return Result.Failure(
                Error.FileSystem("UPDATER_NOT_FOUND", "更新程序不存在", filePath: updaterPath));
        }

        EnsureUpdaterDirectoryPermissions(updaterPath);

        try
        {
            Process.Start(new ProcessStartInfo {
                FileName = updaterPath,
                Arguments = $"-I --source {sourceId}",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            });
            Application.Current?.Shutdown();
            return Result.Success();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Result.Failure(Error.Permission("UPDATER_ELEVATION_CANCELLED", "已取消管理员权限授权，更新已中止", ex: ex));
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(AppUpdateService), ex, "启动更新程序失败: {UpdaterPath}", updaterPath);
            return Result.Failure(Error.Unknown("UPDATER_START_FAILED", ex.Message, ex));
        }
    }

    private UpdateCandidate? SelectCandidateVersion(UpdateNotice notice, bool includePrerelease)
    {
        UpdateCandidate? stableCandidate = null;
        if (!string.IsNullOrWhiteSpace(notice.Stable?.Version))
        {
            stableCandidate = new UpdateCandidate {
                Version = notice.Stable!.Version!,
                Source = notice.Stable.Source,
                Notes = notice.Stable.Notes,
                IsPrerelease = false
            };
        }

        if (!includePrerelease || string.IsNullOrWhiteSpace(notice.Alpha?.Version))
        {
            return stableCandidate;
        }

        var alphaCandidate = new UpdateCandidate {
            Version = notice.Alpha!.Version!,
            Source = notice.Alpha.Source,
            Notes = notice.Alpha.Notes,
            IsPrerelease = true
        };

        if (stableCandidate == null)
        {
            return alphaCandidate;
        }

        return CompareSemVer(alphaCandidate.Version, stableCandidate.Version) > 0
            ? alphaCandidate
            : stableCandidate;
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = assembly.GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static int CompareSemVer(string left, string right)
    {
        if (!TryParseSemVer(left, out var leftVersion) || !TryParseSemVer(right, out var rightVersion))
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        var coreCompare = leftVersion.CompareCore(rightVersion);
        if (coreCompare != 0)
        {
            return coreCompare;
        }

        var leftPre = leftVersion.PreRelease;
        var rightPre = rightVersion.PreRelease;
        if (leftPre.Length == 0 && rightPre.Length == 0)
        {
            return 0;
        }

        if (leftPre.Length == 0)
        {
            return 1;
        }

        if (rightPre.Length == 0)
        {
            return -1;
        }

        var count = Math.Max(leftPre.Length, rightPre.Length);
        for (var i = 0; i < count; i++)
        {
            if (i >= leftPre.Length)
            {
                return -1;
            }

            if (i >= rightPre.Length)
            {
                return 1;
            }

            var leftId = leftPre[i];
            var rightId = rightPre[i];
            var leftIsNumber = int.TryParse(leftId, out var leftNum);
            var rightIsNumber = int.TryParse(rightId, out var rightNum);

            int idCompare;
            if (leftIsNumber && rightIsNumber)
            {
                idCompare = leftNum.CompareTo(rightNum);
            }
            else if (leftIsNumber)
            {
                idCompare = -1;
            }
            else if (rightIsNumber)
            {
                idCompare = 1;
            }
            else
            {
                idCompare = string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
            }

            if (idCompare != 0)
            {
                return idCompare;
            }
        }

        return 0;
    }

    private static bool TryParseSemVer(string version, out SemanticVersion semanticVersion)
    {
        semanticVersion = default;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalized = version.Trim().TrimStart('v', 'V');
        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        var dashIndex = normalized.IndexOf('-');
        var corePart = dashIndex >= 0 ? normalized[..dashIndex] : normalized;
        var prePart = dashIndex >= 0 ? normalized[(dashIndex + 1)..] : string.Empty;

        var core = corePart.Split('.');
        if (core.Length < 3)
        {
            return false;
        }

        if (!int.TryParse(core[0], out var major) ||
            !int.TryParse(core[1], out var minor) ||
            !int.TryParse(core[2], out var patch))
        {
            return false;
        }

        var preRelease = string.IsNullOrWhiteSpace(prePart)
            ? Array.Empty<string>()
            : prePart.Split('.').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        semanticVersion = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    private static string ResolveSourceId(UpdateCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.Source))
        {
            return candidate.Source!;
        }

        return candidate.Version.Contains("-alpha", StringComparison.OrdinalIgnoreCase)
            ? "cnb-alpha"
            : "cnb";
    }

    private void EnsureUpdaterDirectoryPermissions(string updaterPath)
    {
        try
        {
            var appDirectory = Path.GetDirectoryName(updaterPath);
            if (string.IsNullOrWhiteSpace(appDirectory) || !Directory.Exists(appDirectory))
            {
                return;
            }

            var info = new DirectoryInfo(appDirectory);
            var access = info.GetAccessControl();
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var rule = new FileSystemAccessRule(
                usersSid,
                FileSystemRights.Modify | FileSystemRights.Synchronize,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            access.AddAccessRule(rule);
            info.SetAccessControl(access);
        }
        catch (Exception ex)
        {
            _logService.Warn(nameof(AppUpdateService), "更新前设置安装目录权限失败: {Message}", ex.Message);
        }
    }

    private sealed class UpdateNotice
    {
        public NoticeVersion? Stable { get; set; }

        public NoticeVersion? Alpha { get; set; }
    }

    private sealed class NoticeVersion
    {
        public string? Version { get; set; }

        public string? Source { get; set; }

        public string? Notes { get; set; }
    }

    private sealed class UpdateCandidate
    {
        public string Version { get; set; } = string.Empty;

        public string? Source { get; set; }

        public string? Notes { get; set; }

        public bool IsPrerelease { get; set; }
    }

    private readonly record struct SemanticVersion(int Major, int Minor, int Patch, string[] PreRelease)
    {
        public int CompareCore(SemanticVersion other)
        {
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }

            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }

            return Patch.CompareTo(other.Patch);
        }
    }
}
