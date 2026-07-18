using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 下载并校验 catalog Manifest v2 声明的 Release 插件包。
/// </summary>
public sealed class PluginPackageService : IPluginPackageService
{
    private const int DownloadBufferSize = 128 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    private readonly HttpClient _httpClient;
    private readonly IDownloadSourceSelector _downloadSourceSelector;

    public PluginPackageService(
        HttpClient httpClient,
        IDownloadSourceSelector downloadSourceSelector)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _downloadSourceSelector =
            downloadSourceSelector ?? throw new ArgumentNullException(nameof(downloadSourceSelector));
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

}
