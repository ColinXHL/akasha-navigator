using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

public class AppUpdateService : IAppUpdateService
{
    private readonly ILogService _logService;
    private readonly IUpdateManifestService _updateManifestService;

    public AppUpdateService(ILogService logService, IUpdateManifestService updateManifestService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _updateManifestService =
            updateManifestService ?? throw new ArgumentNullException(nameof(updateManifestService));
    }

    public async Task<Result<AppUpdateCheckResult>> CheckForUpdateAsync(bool includePrerelease)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            var manifestResult = await _updateManifestService.RefreshAsync();
            if (manifestResult.IsFailure)
            {
                return Result<AppUpdateCheckResult>.Failure(
                    manifestResult.Error ??
                    Error.Unknown("UPDATE_MANIFEST_RESULT_INVALID", "更新清单服务返回失败但未提供错误"));
            }

            var candidate = SelectCandidateVersion(manifestResult.Value!, includePrerelease);
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

        try
        {
            var arguments = string.Equals(sourceId, "cnb", StringComparison.OrdinalIgnoreCase)
                ? "-I"
                : $"-I --source {sourceId}";
            Process.Start(updaterPath, arguments);
            Application.Current?.Shutdown();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(AppUpdateService), ex, "启动更新程序失败: {UpdaterPath}", updaterPath);
            return Result.Failure(Error.Unknown("UPDATER_START_FAILED", ex.Message, ex));
        }
    }

    private UpdateCandidate? SelectCandidateVersion(UpdateManifest manifest, bool includePrerelease)
    {
        UpdateCandidate? stableCandidate = null;
        if (!string.IsNullOrWhiteSpace(manifest.Stable?.Version))
        {
            stableCandidate = new UpdateCandidate {
                Version = manifest.Stable!.Version!,
                Notes = manifest.Stable.Notes,
                IsPrerelease = false
            };
        }

        if (!includePrerelease || string.IsNullOrWhiteSpace(manifest.Alpha?.Version))
        {
            return stableCandidate;
        }

        var alphaCandidate = new UpdateCandidate {
            Version = manifest.Alpha!.Version!,
            Notes = manifest.Alpha.Notes,
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
        // 注意：NoticeVersion.Source 是下载源（如 "qiniu"），不是 kachina 更新频道 ID。
        // kachina 只识别 "cnb"、"cnb-alpha"、"github"，必须根据 IsPrerelease 决定频道。
        return candidate.IsPrerelease
            ? "cnb-alpha"
            : "cnb";
    }

    private sealed class UpdateCandidate
    {
        public string Version { get; set; } = string.Empty;

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
