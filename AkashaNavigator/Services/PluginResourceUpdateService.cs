using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 下载、校验并原子替换插件独立资源。
/// </summary>
public sealed class PluginResourceUpdateService : IPluginResourceUpdateService
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
    private const string PickBlacklistDirectoryName = "pick-blacklist";
    private const string CurrentFileName = "current.json";
    private const string StateFileName = "state.json";

    private readonly HttpClient _httpClient;
    private readonly IUpdateManifestService _manifestService;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly ICompanionProcessManager _companionProcessManager;
    private readonly ILogService _logService;
    private readonly string _resourceRoot;
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    public PluginResourceUpdateService(
        HttpClient httpClient,
        IUpdateManifestService manifestService,
        IPluginLibrary pluginLibrary,
        ICompanionProcessManager companionProcessManager,
        ILogService logService)
        : this(
            httpClient,
            manifestService,
            pluginLibrary,
            companionProcessManager,
            logService,
            AppPaths.PluginResourcesDirectory)
    {
    }

    internal PluginResourceUpdateService(
        HttpClient httpClient,
        IUpdateManifestService manifestService,
        IPluginLibrary pluginLibrary,
        ICompanionProcessManager companionProcessManager,
        ILogService logService,
        string resourceRoot)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _companionProcessManager =
            companionProcessManager ?? throw new ArgumentNullException(nameof(companionProcessManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _resourceRoot = resourceRoot ?? throw new ArgumentNullException(nameof(resourceRoot));
    }

    public async Task<Result<PluginResourceUpdateResult>> UpdatePickBlacklistAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _updateGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            return Result<PluginResourceUpdateResult>.Failure(
                Error.Network(
                    PluginResourceErrorCodes.DownloadCanceled,
                    "拾取黑名单更新已取消",
                    ex));
        }

        try
        {
            var manifest = _manifestService.Current;
            if (manifest == null)
            {
                return Success(PluginResourceUpdateStatus.ManifestUnavailable);
            }

            if (!manifest.Plugins.TryGetValue(AppConstants.AutomationPluginId, out var remotePlugin) ||
                !remotePlugin.Resources.TryGetValue(
                    AppConstants.PickBlacklistResourceKey,
                    out var resource))
            {
                return Success(PluginResourceUpdateStatus.ResourceUnavailable);
            }

            var installed = _pluginLibrary.GetInstalledPluginInfo(AppConstants.AutomationPluginId);
            if (installed == null)
            {
                return Success(PluginResourceUpdateStatus.PluginNotInstalled);
            }

            if (PluginLibrary.CompareVersions(installed.Version, resource.MinPluginVersion) < 0)
            {
                return Success(PluginResourceUpdateStatus.PluginVersionTooLow);
            }

            if (!IsValidMetadata(resource, out var resourceUri))
            {
                return Failure(
                    PluginResourceErrorCodes.MetadataInvalid,
                    "远程拾取黑名单元数据无效");
            }

            var resourceDirectory = Path.Combine(
                _resourceRoot,
                AppConstants.AutomationPluginId,
                PickBlacklistDirectoryName);
            var currentPath = Path.Combine(resourceDirectory, CurrentFileName);
            var statePath = Path.Combine(resourceDirectory, StateFileName);

            if (File.Exists(currentPath) &&
                string.Equals(
                    await ComputeSha256Async(currentPath, cancellationToken),
                    resource.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                var stateResult = await SaveStateAsync(statePath, resource, cancellationToken);
                return stateResult.IsFailure
                    ? Result<PluginResourceUpdateResult>.Failure(stateResult.Error!)
                    : Success(PluginResourceUpdateStatus.UpToDate, currentPath);
            }

            Directory.CreateDirectory(resourceDirectory);
            var temporaryPath = Path.Combine(
                resourceDirectory,
                $".{CurrentFileName}.{Guid.NewGuid():N}.download");

            try
            {
                var downloadResult = await DownloadAsync(
                    resourceUri!,
                    resource,
                    temporaryPath,
                    cancellationToken);
                if (downloadResult.IsFailure)
                {
                    return Result<PluginResourceUpdateResult>.Failure(downloadResult.Error!);
                }

                var contentResult = await ValidateContentAsync(
                    temporaryPath,
                    resource.EntryCount,
                    cancellationToken);
                if (contentResult.IsFailure)
                {
                    return Result<PluginResourceUpdateResult>.Failure(contentResult.Error!);
                }

                File.Move(temporaryPath, currentPath, overwrite: true);
                var stateResult = await SaveStateAsync(statePath, resource, cancellationToken);
                if (stateResult.IsFailure)
                {
                    _logService.Warn(
                        nameof(PluginResourceUpdateService),
                        "拾取黑名单已更新，但状态文件保存失败: {ErrorMessage}",
                        stateResult.Error?.Message ?? "未知错误");
                }

                var workerRunning =
                    _companionProcessManager.GetStatus(AppConstants.AutomationPluginId).Running;
                _logService.Info(
                    nameof(PluginResourceUpdateService),
                    workerRunning
                        ? "拾取黑名单已更新，将在 Automation Worker 下次启动时生效"
                        : "拾取黑名单已更新");
                return Success(
                    PluginResourceUpdateStatus.Updated,
                    currentPath,
                    workerRunning);
            }
            catch (OperationCanceledException ex)
            {
                return Result<PluginResourceUpdateResult>.Failure(
                    Error.Network(
                        PluginResourceErrorCodes.DownloadCanceled,
                        "拾取黑名单下载已取消或超时",
                        ex,
                        resource.Url));
            }
            catch (HttpRequestException ex)
            {
                return Result<PluginResourceUpdateResult>.Failure(
                    Error.Network(
                        PluginResourceErrorCodes.DownloadFailed,
                        $"拾取黑名单下载失败: {ex.Message}",
                        ex,
                        resource.Url));
            }
            catch (Exception ex)
            {
                return Result<PluginResourceUpdateResult>.Failure(
                    Error.FileSystem(
                        PluginResourceErrorCodes.SaveFailed,
                        $"保存拾取黑名单失败: {ex.Message}",
                        ex,
                        currentPath));
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private async Task<Result> DownloadAsync(
        Uri resourceUri,
        PluginResourceInfo resource,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(DownloadTimeout);
        using var response = await _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, resourceUri),
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long totalBytes = 0;
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, timeoutSource.Token);
            if (bytesRead == 0)
                break;

            totalBytes += bytesRead;
            if (totalBytes > resource.Size)
            {
                return Result.Failure(
                    Error.Validation(
                        PluginResourceErrorCodes.SizeMismatch,
                        $"拾取黑名单大小超过清单值 {resource.Size} 字节"));
            }

            hash.AppendData(buffer, 0, bytesRead);
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), timeoutSource.Token);
        }

        await destination.FlushAsync(timeoutSource.Token);
        if (totalBytes != resource.Size)
        {
            return Result.Failure(
                Error.Validation(
                    PluginResourceErrorCodes.SizeMismatch,
                    $"拾取黑名单大小不匹配：预期 {resource.Size}，实际 {totalBytes}"));
        }

        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actualHash, resource.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(
                Error.Validation(
                    PluginResourceErrorCodes.HashMismatch,
                    "拾取黑名单 SHA-256 校验失败"));
        }

        return Result.Success();
    }

    private static async Task<Result> ValidateContentAsync(
        string filePath,
        int expectedEntryCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var entries = await JsonSerializer.DeserializeAsync<string[]>(
                stream,
                cancellationToken: cancellationToken);
            if (entries == null ||
                entries.Any(string.IsNullOrWhiteSpace) ||
                entries.Length != expectedEntryCount)
            {
                return Result.Failure(
                    Error.Serialization(
                        PluginResourceErrorCodes.ContentInvalid,
                        $"拾取黑名单内容无效或条目数不匹配，预期 {expectedEntryCount}"));
            }

            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(
                Error.Serialization(
                    PluginResourceErrorCodes.ContentInvalid,
                    "拾取黑名单不是有效的字符串数组",
                    ex,
                    filePath));
        }
    }

    private static async Task<Result> SaveStateAsync(
        string statePath,
        PluginResourceInfo resource,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            var temporaryPath = $"{statePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                var json = JsonSerializer.Serialize(
                    new PluginResourceState
                    {
                        Version = resource.Version,
                        Sha256 = resource.Sha256.ToLowerInvariant(),
                        EntryCount = resource.EntryCount
                    },
                    JsonHelper.WriteOptions);
                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, statePath, overwrite: true);
                return Result.Success();
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(
                Error.FileSystem(
                    PluginResourceErrorCodes.SaveFailed,
                    $"保存拾取黑名单状态失败: {ex.Message}",
                    ex,
                    statePath));
        }
    }

    private static bool IsValidMetadata(PluginResourceInfo resource, out Uri? resourceUri)
    {
        resourceUri = null;
        return resource.Size > 0 &&
               resource.EntryCount > 0 &&
               resource.Sha256.Length == 64 &&
               resource.Sha256.All(Uri.IsHexDigit) &&
               Uri.TryCreate(resource.Url, UriKind.Absolute, out resourceUri) &&
               resourceUri.Scheme == Uri.UriSchemeHttps;
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Result<PluginResourceUpdateResult> Success(
        PluginResourceUpdateStatus status,
        string? filePath = null,
        bool takesEffectOnNextWorkerStart = false)
    {
        return Result<PluginResourceUpdateResult>.Success(
            new PluginResourceUpdateResult(status, filePath, takesEffectOnNextWorkerStart));
    }

    private static Result<PluginResourceUpdateResult> Failure(string code, string message)
    {
        return Result<PluginResourceUpdateResult>.Failure(Error.Validation(code, message));
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
        }
    }
}
