using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 从共享 Manifest 下载、校验并安装远程插件包。
/// </summary>
public sealed class PluginPackageService : IPluginPackageService
{
    private const int DownloadBufferSize = 128 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    private readonly HttpClient _httpClient;
    private readonly IUpdateManifestService _updateManifestService;
    private readonly IDownloadSourceSelector _downloadSourceSelector;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly Func<string> _hostVersionProvider;

    public PluginPackageService(
        HttpClient httpClient,
        IUpdateManifestService updateManifestService,
        IDownloadSourceSelector downloadSourceSelector,
        IPluginLibrary pluginLibrary)
        : this(
            httpClient,
            updateManifestService,
            downloadSourceSelector,
            pluginLibrary,
            GetCurrentHostVersion)
    {
    }

    internal PluginPackageService(
        HttpClient httpClient,
        IUpdateManifestService updateManifestService,
        IDownloadSourceSelector downloadSourceSelector,
        IPluginLibrary pluginLibrary,
        Func<string> hostVersionProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _updateManifestService =
            updateManifestService ?? throw new ArgumentNullException(nameof(updateManifestService));
        _downloadSourceSelector =
            downloadSourceSelector ?? throw new ArgumentNullException(nameof(downloadSourceSelector));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _hostVersionProvider =
            hostVersionProvider ?? throw new ArgumentNullException(nameof(hostVersionProvider));
    }

    public IReadOnlyList<PluginCatalogEntry> GetRemoteCatalog()
    {
        var plugins = _updateManifestService.Current?.Plugins;
        if (plugins == null)
        {
            return Array.Empty<PluginCatalogEntry>();
        }

        return plugins
            .Where(item => item.Value.Package != null)
            .Select(
                item => new PluginCatalogEntry {
                    Id = item.Key,
                    Name = string.IsNullOrWhiteSpace(item.Value.Name) ? item.Key : item.Value.Name,
                    Version = item.Value.Version,
                    IsRemote = true,
                    MinHostVersion = item.Value.MinHostVersion,
                    Package = item.Value.Package
                })
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public bool IsUpdateAvailable(string pluginId)
    {
        var remote = GetRemotePlugin(pluginId);
        var installed = _pluginLibrary.GetInstalledPluginInfo(pluginId);
        return remote != null &&
               installed != null &&
               PluginLibrary.CompareVersions(remote.Version, installed.Version) > 0;
    }

    public async Task<Result<InstalledPluginInfo>> InstallOrUpdateAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Validation("REMOTE_PLUGIN_ID_EMPTY", "远程插件 ID 不能为空"));
        }

        var manifestResult = await _updateManifestService.RefreshAsync(cancellationToken);
        if (manifestResult.IsFailure)
        {
            return Result<InstalledPluginInfo>.Failure(manifestResult.Error!);
        }

        var remote = GetRemotePlugin(pluginId);
        if (remote?.Package == null)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.RemotePackageNotFound,
                    $"远程插件目录中不存在 {pluginId}",
                    pluginId: pluginId));
        }

        var hostVersion = _hostVersionProvider();
        if (!string.IsNullOrWhiteSpace(remote.MinHostVersion) &&
            PluginLibrary.CompareVersions(hostVersion, remote.MinHostVersion) < 0)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.HostVersionTooLow,
                    $"插件需要 AkashaNavigator {remote.MinHostVersion} 或更高版本，当前版本为 {hostVersion}",
                    pluginId: pluginId));
        }

        var installed = _pluginLibrary.GetInstalledPluginInfo(pluginId);
        if (installed != null &&
            PluginLibrary.CompareVersions(remote.Version, installed.Version) <= 0)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.VersionNotNewer,
                    $"远程版本 {remote.Version} 不高于已安装版本 {installed.Version}",
                    pluginId: pluginId));
        }

        var downloadResult = await DownloadPackageAsync(
            pluginId,
            remote.Package,
            progress,
            cancellationToken);
        if (downloadResult.IsFailure)
        {
            return Result<InstalledPluginInfo>.Failure(downloadResult.Error!);
        }

        using var download = downloadResult.Value!;
        return _pluginLibrary.InstallPluginPackage(download.FilePath);
    }

    public async Task<Result<DownloadedPluginPackage>> DownloadPackageAsync(
        string pluginId,
        PluginPackageInfo package,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!PluginIdValidator.IsValid(pluginId) ||
            package == null ||
            string.IsNullOrWhiteSpace(package.FileName) ||
            !string.Equals(
                Path.GetFileName(package.FileName),
                package.FileName,
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(package.FileName),
                ".zip",
                StringComparison.OrdinalIgnoreCase) ||
            package.Size <= 0 ||
            !IsSha256(package.Sha256))
        {
            return Result<DownloadedPluginPackage>.Failure(
                Error.Validation(
                    PluginErrorCodes.RemotePackageNotFound,
                    "插件 Release 包元数据无效"));
        }

        Result<IReadOnlyList<DownloadSourceInfo>> sourcesResult;
        try
        {
            sourcesResult =
                await _downloadSourceSelector.GetOrderedSourcesAsync(package, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            return Result<DownloadedPluginPackage>.Failure(
                Error.Plugin(
                    PluginErrorCodes.RemoteDownloadCanceled,
                    "插件下载已取消",
                    ex,
                    pluginId));
        }

        if (sourcesResult.IsFailure)
        {
            return Result<DownloadedPluginPackage>.Failure(sourcesResult.Error!);
        }

        var attemptErrors = new List<string>();
        foreach (var source in sourcesResult.Value!)
        {
            var temporaryPath = Path.Combine(
                Path.GetTempPath(),
                $"AkashaNavigator.PluginDownload.{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadAsync(
                    source,
                    package,
                    temporaryPath,
                    progress,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return Result<DownloadedPluginPackage>.Success(
                    new DownloadedPluginPackage(temporaryPath, source.Id));
            }
            catch (OperationCanceledException ex)
            {
                TryDeleteFile(temporaryPath);
                return Result<DownloadedPluginPackage>.Failure(
                    Error.Plugin(
                        PluginErrorCodes.RemoteDownloadCanceled,
                        "插件下载已取消",
                        ex,
                        pluginId));
            }
            catch (Exception ex)
            {
                attemptErrors.Add($"{source.Id}: {SimplifyError(ex)}");
                TryDeleteFile(temporaryPath);
            }
        }

        var error = Error.Plugin(
            PluginErrorCodes.RemoteDownloadFailed,
            $"插件下载失败，已尝试：{string.Join("；", attemptErrors)}",
            pluginId: pluginId);
        error.Metadata["AttemptedSources"] = sourcesResult.Value!.Select(source => source.Id).ToArray();
        return Result<DownloadedPluginPackage>.Failure(error);
    }

    private RemotePluginInfo? GetRemotePlugin(string pluginId)
    {
        var plugins = _updateManifestService.Current?.Plugins;
        if (plugins == null)
        {
            return null;
        }

        return plugins.FirstOrDefault(
                item => string.Equals(item.Key, pluginId, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private async Task DownloadAsync(
        DownloadSourceInfo source,
        PluginPackageInfo package,
        string destinationPath,
        IProgress<PluginDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(DownloadTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                DownloadBufferSize,
                useAsync: true);

            var buffer = new byte[DownloadBufferSize];
            long received = 0;
            var progressStopwatch = Stopwatch.StartNew();
            progress?.Report(
                new PluginDownloadProgress {
                    SourceId = source.Id,
                    BytesReceived = 0,
                    TotalBytes = package.Size
                });

            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), timeoutSource.Token);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), timeoutSource.Token);
                received += read;
                if (progressStopwatch.ElapsedMilliseconds >= 100)
                {
                    progress?.Report(
                        new PluginDownloadProgress {
                            SourceId = source.Id,
                            BytesReceived = received,
                            TotalBytes = package.Size
                        });
                    progressStopwatch.Restart();
                }
            }

            await output.FlushAsync(timeoutSource.Token);
            progress?.Report(
                new PluginDownloadProgress {
                    SourceId = source.Id,
                    BytesReceived = received,
                    TotalBytes = package.Size
                });

            if (received != package.Size)
            {
                throw new InvalidDataException(
                    $"文件大小不一致，预期 {package.Size} 字节，实际 {received} 字节");
            }

            output.Position = 0;
            var digest = await SHA256.HashDataAsync(output, timeoutSource.Token);
            var actualHash = Convert.ToHexString(digest).ToLowerInvariant();
            if (!string.Equals(actualHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("SHA-256 校验不一致");
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"下载源 {source.Id} 在 15 分钟内未完成", ex);
        }
    }

    private static string SimplifyError(Exception exception)
    {
        return exception switch {
            HttpRequestException => "网络请求失败",
            TimeoutException => "下载超时",
            InvalidDataException invalidData => invalidData.Message,
            IOException => "临时文件读写失败",
            _ => exception.Message
        };
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 临时下载文件清理是 best effort。
        }
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(
                   character =>
                       character is >= '0' and <= '9' or
                           >= 'a' and <= 'f');
    }

    private static string GetCurrentHostVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = assembly.GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
