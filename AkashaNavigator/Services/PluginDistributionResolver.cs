using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

public sealed class PluginDistributionResolver : IPluginDistributionResolver
{
    private readonly IPluginPackageService? _pluginPackageService;

    public PluginDistributionResolver(
        IPluginPackageService pluginPackageService)
    {
        _pluginPackageService =
            pluginPackageService ??
            throw new ArgumentNullException(nameof(pluginPackageService));
    }

    private PluginDistributionResolver()
    {
    }

    internal static PluginDistributionResolver CreateUnavailable() => new();

    public async Task<Result<ResolvedPluginDistribution>> ResolveAsync(
        string pluginId,
        PluginRepositoryEntry entry,
        CatalogPluginManifest manifest,
        string repositorySourceDirectory,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginDistribution.{pluginId}.{Guid.NewGuid():N}");
        var handedOff = false;
        try
        {
            if (string.Equals(
                    entry.DistributionType,
                    AppConstants.PluginDistributionRepository,
                    StringComparison.Ordinal))
            {
                CopyDirectorySecure(
                    repositorySourceDirectory,
                    stagingDirectory);
                var resolved = new ResolvedPluginDistribution(
                    stagingDirectory);
                handedOff = true;
                return Result<ResolvedPluginDistribution>.Success(resolved);
            }

            if (!string.Equals(
                    entry.DistributionType,
                    AppConstants.PluginDistributionRelease,
                    StringComparison.Ordinal) ||
                _pluginPackageService == null)
            {
                return Result<ResolvedPluginDistribution>.Failure(
                    Error.Plugin(
                        PluginErrorCodes.DistributionUnsupported,
                        "插件分发类型不受支持或 Release 下载服务不可用",
                        pluginId: pluginId));
            }

            var downloadResult =
                await _pluginPackageService.DownloadPackageAsync(
                        pluginId,
                        CreateReleasePackage(manifest),
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (downloadResult.IsFailure)
            {
                return Result<ResolvedPluginDistribution>.Failure(
                    downloadResult.Error!);
            }

            using var download = downloadResult.Value!;
            var pluginDirectory = PluginReleaseArchiveExtractor.Extract(
                download.FilePath,
                stagingDirectory);
            var releaseResolved = new ResolvedPluginDistribution(
                pluginDirectory,
                stagingDirectory);
            handedOff = true;
            return Result<ResolvedPluginDistribution>.Success(
                releaseResolved);
        }
        catch (OperationCanceledException ex)
        {
            return Result<ResolvedPluginDistribution>.Failure(
                Error.Plugin(
                    PluginErrorCodes.RemoteDownloadCanceled,
                    "插件分发准备已取消",
                    ex,
                    pluginId));
        }
        catch (Exception ex)
        {
            return Result<ResolvedPluginDistribution>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.InstallTransactionFailed,
                    $"准备插件分发内容失败: {ex.Message}",
                    ex,
                    repositorySourceDirectory));
        }
        finally
        {
            if (!handedOff)
            {
                TryDeleteDirectory(stagingDirectory);
            }
        }
    }

    private static PluginPackageInfo CreateReleasePackage(
        CatalogPluginManifest manifest)
    {
        var tag = Uri.EscapeDataString(manifest.Distribution.Tag!);
        var asset = Uri.EscapeDataString(manifest.Distribution.Asset!);
        return new PluginPackageInfo {
            FileName = manifest.Distribution.Asset!,
            Size = manifest.Distribution.Size!.Value,
            Sha256 = manifest.Distribution.Sha256!,
            Sources = new List<DownloadSourceInfo> {
                new() {
                    Id = "github",
                    Url = string.Format(
                        AppConstants.OfficialPluginReleaseGitHubUrlFormat,
                        tag,
                        asset)
                },
                new() {
                    Id = "cnb",
                    Url = string.Format(
                        AppConstants.OfficialPluginReleaseCnbUrlFormat,
                        tag,
                        asset)
                }
            }
        };
    }

    private static void CopyDirectorySecure(
        string sourceDirectory,
        string targetDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException(sourceDirectory);
        }

        if ((source.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("插件目录不能是重解析点");
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in source.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"插件包含重解析文件: {file.Name}");
            }

            file.CopyTo(
                Path.Combine(targetDirectory, file.Name),
                overwrite: true);
        }

        foreach (var directory in source.EnumerateDirectories())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"插件包含重解析目录: {directory.Name}");
            }

            CopyDirectorySecure(
                directory.FullName,
                Path.Combine(targetDirectory, directory.Name));
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // staging 目录由系统后续清理。
        }
    }
}
