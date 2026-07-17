using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 更新清单下载与本地缓存服务。
/// </summary>
public sealed class UpdateManifestService : IUpdateManifestService
{
    private readonly HttpClient _httpClient;
    private readonly UpdateOptions _options;
    private readonly ILogService _logService;
    private readonly string _cacheFilePath;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private UpdateManifest? _current;
    private UpdateManifestState? _httpState;
    public UpdateManifestService(HttpClient httpClient, UpdateOptions options, ILogService logService)
        : this(httpClient, options, logService, AppPaths.NoticeCacheFilePath, AppPaths.NoticeStateFilePath)
    {
    }

    internal UpdateManifestService(
        HttpClient httpClient,
        UpdateOptions options,
        ILogService logService,
        string cacheFilePath,
        string stateFilePath)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _cacheFilePath = cacheFilePath ?? throw new ArgumentNullException(nameof(cacheFilePath));
        _stateFilePath = stateFilePath ?? throw new ArgumentNullException(nameof(stateFilePath));

        LoadLocalCache();
    }

    public UpdateManifest? Current => Volatile.Read(ref _current);

    public async Task<Result<UpdateManifest>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var lockTaken = false;
        try
        {
            await _refreshLock.WaitAsync(cancellationToken);
            lockTaken = true;

            if (!Uri.TryCreate(_options.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
                manifestUri.Scheme != Uri.UriSchemeHttps)
            {
                return UseCacheOrFailure(
                    Error.Configuration(
                        "UPDATE_MANIFEST_URL_INVALID",
                        $"更新清单地址必须是有效的 HTTPS URL: {_options.ManifestUrl}"));
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
            AddConditionalHeaders(request);
            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_options.RequestTimeout);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                var cached = Current;
                if (cached != null)
                {
                    _logService.Debug(nameof(UpdateManifestService), "更新清单未变化，继续使用本地缓存");
                    return Result<UpdateManifest>.Success(cached);
                }

                return Result<UpdateManifest>.Failure(
                    Error.Network(
                        "UPDATE_MANIFEST_NOT_MODIFIED_WITHOUT_CACHE",
                        "服务器返回 304，但本地没有可用的更新清单缓存",
                        url: _options.ManifestUrl));
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            UpdateManifest? manifest;
            try
            {
                manifest = JsonHelper.Deserialize<UpdateManifest>(json);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return UseCacheOrFailure(
                    Error.Serialization(
                        "UPDATE_MANIFEST_JSON_INVALID",
                        "更新清单 JSON 解析失败",
                        ex,
                        _options.ManifestUrl));
            }

            if (!IsValid(manifest))
            {
                return UseCacheOrFailure(
                    Error.Serialization(
                        "UPDATE_MANIFEST_CONTENT_INVALID",
                        "更新清单未包含有效的应用版本信息",
                        filePath: _options.ManifestUrl));
            }

            var cacheSaveResult = await SaveAtomicallyAsync(_cacheFilePath, manifest!, cancellationToken);
            if (cacheSaveResult.IsFailure)
            {
                return UseCacheOrFailure(cacheSaveResult.Error!);
            }

            Volatile.Write(ref _current, manifest);

            var newState = CreateState(response);
            var stateSaveResult = await SaveAtomicallyAsync(_stateFilePath, newState, cancellationToken);
            if (stateSaveResult.IsSuccess)
            {
                _httpState = newState;
            }
            else
            {
                _logService.Warn(
                    nameof(UpdateManifestService),
                    "更新清单已缓存，但 HTTP 状态保存失败: {ErrorMessage}",
                    stateSaveResult.Error?.Message ?? "未知错误");
            }

            return Result<UpdateManifest>.Success(manifest!);
        }
        catch (OperationCanceledException ex)
        {
            return UseCacheOrFailure(
                Error.Network(
                    "UPDATE_MANIFEST_REQUEST_CANCELED",
                    "更新清单请求已取消或超时",
                    ex,
                    _options.ManifestUrl));
        }
        catch (HttpRequestException ex)
        {
            return UseCacheOrFailure(
                Error.Network(
                    "UPDATE_MANIFEST_REQUEST_FAILED",
                    ex.Message,
                    ex,
                    _options.ManifestUrl));
        }
        catch (Exception ex)
        {
            return UseCacheOrFailure(
                Error.Unknown("UPDATE_MANIFEST_REFRESH_UNKNOWN", ex.Message, ex));
        }
        finally
        {
            if (lockTaken)
            {
                _refreshLock.Release();
            }
        }
    }

    private void LoadLocalCache()
    {
        var cacheResult = JsonHelper.LoadFromFile<UpdateManifest>(_cacheFilePath);
        if (cacheResult.IsSuccess && IsValid(cacheResult.Value))
        {
            Volatile.Write(ref _current, cacheResult.Value);

            var stateResult = JsonHelper.LoadFromFile<UpdateManifestState>(_stateFilePath);
            if (stateResult.IsSuccess)
            {
                _httpState = stateResult.Value;
            }

            return;
        }

        if (File.Exists(_cacheFilePath))
        {
            _logService.Warn(
                nameof(UpdateManifestService),
                "本地更新清单缓存无效，将等待远程刷新: {ErrorMessage}",
                cacheResult.Error?.Message ?? "清单内容无效");
        }
    }

    private void AddConditionalHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_httpState?.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", _httpState.ETag);
        }

        if (DateTimeOffset.TryParse(_httpState?.LastModified, out var lastModified))
        {
            request.Headers.IfModifiedSince = lastModified;
        }
    }

    private Result<UpdateManifest> UseCacheOrFailure(Error error)
    {
        var cached = Current;
        if (cached == null)
        {
            return Result<UpdateManifest>.Failure(error);
        }

        _logService.Warn(
            nameof(UpdateManifestService),
            "更新清单刷新失败，继续使用本地缓存: {ErrorMessage}",
            error.Message);
        return Result<UpdateManifest>.Success(cached);
    }

    private static bool IsValid(UpdateManifest? manifest)
    {
        return manifest != null &&
               (!string.IsNullOrWhiteSpace(manifest.Stable?.Version) ||
                !string.IsNullOrWhiteSpace(manifest.Alpha?.Version));
    }

    private static UpdateManifestState CreateState(HttpResponseMessage response)
    {
        return new UpdateManifestState {
            ETag = response.Headers.ETag?.ToString(),
            LastModified = response.Content.Headers.LastModified?.ToString("R")
        };
    }

    private static async Task<Result> SaveAtomicallyAsync<T>(
        string filePath,
        T value,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonHelper.Serialize(value);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
            return Result.Success();
        }
        catch (OperationCanceledException ex)
        {
            return Result.Failure(
                Error.FileSystem("UPDATE_MANIFEST_WRITE_CANCELED", "更新清单缓存写入已取消", ex, filePath));
        }
        catch (IOException ex)
        {
            return Result.Failure(
                Error.FileSystem("UPDATE_MANIFEST_WRITE_FAILED", ex.Message, ex, filePath));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure(
                Error.Permission(
                    "UPDATE_MANIFEST_WRITE_DENIED",
                    $"无权限写入更新清单缓存: {filePath}",
                    ex: ex));
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // 临时文件清理是 best effort，不覆盖原始写入结果。
            }
        }
    }
}
