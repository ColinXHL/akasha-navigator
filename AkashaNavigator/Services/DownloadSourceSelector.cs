using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 通过实际安装包的限时分段读取测量下载源，并在应用更新与插件下载之间共享排序结果。
/// </summary>
public sealed class DownloadSourceSelector : IDownloadSourceSelector
{
    private const int ProbeByteCount = 8 * 1024 * 1024;
    private const int ProbeWarmupByteCount = 1024 * 1024;
    private const int MinimumPartialProbeByteCount = 256 * 1024;
    private const int MinimumSteadyStateByteCount = 512 * 1024;
    private static readonly TimeSpan ProbeTransferDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ConcurrentDictionary<string, CachedMeasurement> _cache = new();
    private readonly ConcurrentDictionary<string, string> _lastSelectedSource = new();
    private CachedSelection? _sharedSelection;

    public DownloadSourceSelector(HttpClient httpClient, IConfigService configService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<Result<IReadOnlyList<DownloadSourceInfo>>> GetOrderedSourcesAsync(
        PluginPackageInfo package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var sources = GetValidSources(package);
        if (sources.Count == 0)
        {
            return Result<IReadOnlyList<DownloadSourceInfo>>.Failure(
                Error.Validation("PLUGIN_DOWNLOAD_SOURCES_EMPTY", "插件包没有可用的 HTTPS 下载源"));
        }

        var preference = _configService.Config.PluginDownloadSourcePreference;
        if (preference != PluginDownloadSourcePreference.Auto)
        {
            var preferredId =
                preference == PluginDownloadSourcePreference.GitHub ? "github" : "cnb";
            return Result<IReadOnlyList<DownloadSourceInfo>>.Success(
                sources
                    .OrderByDescending(
                        source => string.Equals(source.Id, preferredId, StringComparison.OrdinalIgnoreCase))
                    .ToList());
        }

        var sharedSelection = Volatile.Read(ref _sharedSelection);
        if (sharedSelection != null &&
            DateTimeOffset.UtcNow - sharedSelection.CreatedAt < CacheLifetime)
        {
            return Result<IReadOnlyList<DownloadSourceInfo>>.Success(
                OrderByIds(sources, sharedSelection.SourceIds));
        }

        var measurementResult = await MeasureSourcesAsync(
            package,
            cancellationToken: cancellationToken);
        if (measurementResult.IsFailure)
        {
            return Result<IReadOnlyList<DownloadSourceInfo>>.Failure(measurementResult.Error!);
        }

        return Result<IReadOnlyList<DownloadSourceInfo>>.Success(
            measurementResult.Value!.Select(result => result.Source).ToList());
    }

    public async Task<Result<IReadOnlyList<DownloadSourceMeasurement>>> MeasureSourcesAsync(
        PluginPackageInfo package,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var sources = GetValidSources(package);
        if (sources.Count == 0)
        {
            return Result<IReadOnlyList<DownloadSourceMeasurement>>.Failure(
                Error.Validation("DOWNLOAD_SOURCES_EMPTY", "没有可用于测速的 HTTPS 下载源"));
        }

        var cacheKey = CreateCacheKey(package, sources);
        if (!forceRefresh &&
            _cache.TryGetValue(cacheKey, out var cached) &&
            DateTimeOffset.UtcNow - cached.CreatedAt < CacheLifetime)
        {
            return Result<IReadOnlyList<DownloadSourceMeasurement>>.Success(cached.Measurements);
        }

        var probeTasks = sources
            .Select(source => ProbeAsync(source, package.Size, cancellationToken))
            .ToArray();
        var probeResults = await Task.WhenAll(probeTasks);
        cancellationToken.ThrowIfCancellationRequested();

        var successful = probeResults
            .Where(result => result.IsSuccess)
            .OrderBy(result => result.EstimatedDownloadTime)
            .ToList();
        if (successful.Count == 0)
        {
            var errors = string.Join(
                "；",
                probeResults.Select(result => $"{result.Source.Id}: {result.ErrorMessage}"));
            return Result<IReadOnlyList<DownloadSourceMeasurement>>.Failure(
                Error.Network(
                    "DOWNLOAD_SOURCE_PROBE_FAILED",
                    $"所有下载源测速失败：{errors}"));
        }

        ApplyHysteresis(cacheKey, successful);

        var orderedResults = successful
            .Concat(probeResults.Where(result => !result.IsSuccess))
            .ToList();
        var orderedIds = orderedResults
            .Select(result => result.Source.Id)
            .ToList();
        var measurements = orderedResults
            .Select(result => result.ToMeasurement())
            .ToList();
        var cachedMeasurement = new CachedMeasurement(
            DateTimeOffset.UtcNow,
            measurements);

        _cache[cacheKey] = cachedMeasurement;
        Volatile.Write(
            ref _sharedSelection,
            new CachedSelection(cachedMeasurement.CreatedAt, orderedIds));
        _lastSelectedSource[cacheKey] = orderedIds[0];

        return Result<IReadOnlyList<DownloadSourceMeasurement>>.Success(measurements);
    }

    public void ClearCache()
    {
        _cache.Clear();
        Volatile.Write(ref _sharedSelection, null);
    }

    private async Task<ProbeResult> ProbeAsync(
        DownloadSourceInfo source,
        long packageSize,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ProbeTimeout);
        var stopwatch = Stopwatch.StartNew();
        var bytesRead = 0;
        var steadyStateStartBytes = 0;
        TimeSpan? firstByteTime = null;
        TimeSpan? steadyStateStartTime = null;
        long? contentLength = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            request.Headers.Range = new RangeHeaderValue(0, ProbeByteCount - 1);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var buffer = new byte[64 * 1024];
            contentLength = response.Content.Headers.ContentRange?.Length;
            while (bytesRead < ProbeByteCount)
            {
                if (firstByteTime.HasValue &&
                    stopwatch.Elapsed - firstByteTime.Value >= ProbeTransferDuration)
                {
                    break;
                }

                var count = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, ProbeByteCount - bytesRead)),
                    timeoutSource.Token);
                if (count == 0)
                {
                    break;
                }

                firstByteTime ??= stopwatch.Elapsed;
                bytesRead += count;
                if (!steadyStateStartTime.HasValue && bytesRead >= ProbeWarmupByteCount)
                {
                    steadyStateStartBytes = bytesRead;
                    steadyStateStartTime = stopwatch.Elapsed;
                }
            }

            stopwatch.Stop();
            return CreateProbeResult(
                source,
                packageSize,
                contentLength,
                bytesRead,
                firstByteTime,
                steadyStateStartBytes,
                steadyStateStartTime,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            if (!cancellationToken.IsCancellationRequested &&
                bytesRead >= MinimumPartialProbeByteCount &&
                firstByteTime.HasValue)
            {
                return CreateProbeResult(
                    source,
                    packageSize,
                    contentLength,
                    bytesRead,
                    firstByteTime,
                    steadyStateStartBytes,
                    steadyStateStartTime,
                    stopwatch.Elapsed);
            }

            return ProbeResult.Failure(
                source,
                cancellationToken.IsCancellationRequested ? "已取消" : "测速超时");
        }
        catch (Exception ex)
        {
            return ProbeResult.Failure(source, ex.Message);
        }
    }

    private static ProbeResult CreateProbeResult(
        DownloadSourceInfo source,
        long packageSize,
        long? contentLength,
        int bytesRead,
        TimeSpan? firstByteTime,
        int steadyStateStartBytes,
        TimeSpan? steadyStateStartTime,
        TimeSpan elapsed)
    {
        if (bytesRead == 0 || !firstByteTime.HasValue)
        {
            return ProbeResult.Failure(source, "未读取到数据");
        }

        var measuredBytes = bytesRead;
        var transferDuration = elapsed - firstByteTime.Value;
        if (steadyStateStartTime.HasValue &&
            bytesRead - steadyStateStartBytes >= MinimumSteadyStateByteCount)
        {
            measuredBytes = bytesRead - steadyStateStartBytes;
            transferDuration = elapsed - steadyStateStartTime.Value;
        }

        var transferSeconds = Math.Max(transferDuration.TotalSeconds, 0.001);
        var bytesPerSecond = measuredBytes / transferSeconds;
        var totalBytes = contentLength is > 0
            ? contentLength.GetValueOrDefault()
            : Math.Max(packageSize, bytesRead);
        var estimatedSeconds =
            firstByteTime.Value.TotalSeconds +
            totalBytes / bytesPerSecond;

        return ProbeResult.Success(
            source,
            bytesRead,
            bytesPerSecond,
            firstByteTime.Value,
            TimeSpan.FromSeconds(estimatedSeconds));
    }

    private void ApplyHysteresis(string cacheKey, List<ProbeResult> successful)
    {
        if (successful.Count < 2 ||
            !_lastSelectedSource.TryGetValue(cacheKey, out var previousSourceId))
        {
            return;
        }

        var fastestSeconds = successful[0].EstimatedDownloadTime.TotalSeconds;
        var secondSeconds = successful[1].EstimatedDownloadTime.TotalSeconds;
        if (fastestSeconds <= 0 ||
            (secondSeconds - fastestSeconds) / fastestSeconds >= 0.20)
        {
            return;
        }

        var previousIndex = successful.FindIndex(
            result => string.Equals(
                result.Source.Id,
                previousSourceId,
                StringComparison.OrdinalIgnoreCase));
        if (previousIndex <= 0)
        {
            return;
        }

        var previous = successful[previousIndex];
        successful.RemoveAt(previousIndex);
        successful.Insert(0, previous);
    }

    private static List<DownloadSourceInfo> GetValidSources(PluginPackageInfo package)
    {
        return package.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Id) &&
                             Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
                             uri.Scheme == Uri.UriSchemeHttps)
            .ToList();
    }

    private static string CreateCacheKey(
        PluginPackageInfo package,
        IEnumerable<DownloadSourceInfo> sources)
    {
        return string.Join(
            "|",
            new[] { package.FileName, package.Size.ToString() }
                .Concat(sources.Select(source => $"{source.Id}:{source.Url}")));
    }

    private static IReadOnlyList<DownloadSourceInfo> OrderByIds(
        IReadOnlyList<DownloadSourceInfo> sources,
        IReadOnlyList<string> orderedIds)
    {
        var order = orderedIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index, StringComparer.OrdinalIgnoreCase);
        return sources
            .OrderBy(source => order.TryGetValue(source.Id, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private sealed record CachedSelection(DateTimeOffset CreatedAt, IReadOnlyList<string> SourceIds);

    private sealed record CachedMeasurement(
        DateTimeOffset CreatedAt,
        IReadOnlyList<DownloadSourceMeasurement> Measurements);

    private sealed record ProbeResult(
        DownloadSourceInfo Source,
        bool IsSuccess,
        long BytesRead,
        double BytesPerSecond,
        TimeSpan TimeToFirstByte,
        TimeSpan EstimatedDownloadTime,
        string ErrorMessage)
    {
        public static ProbeResult Success(
            DownloadSourceInfo source,
            long bytesRead,
            double bytesPerSecond,
            TimeSpan timeToFirstByte,
            TimeSpan estimatedDownloadTime)
        {
            return new ProbeResult(
                source,
                true,
                bytesRead,
                bytesPerSecond,
                timeToFirstByte,
                estimatedDownloadTime,
                string.Empty);
        }

        public static ProbeResult Failure(DownloadSourceInfo source, string errorMessage)
        {
            return new ProbeResult(
                source,
                false,
                0,
                0,
                TimeSpan.Zero,
                TimeSpan.MaxValue,
                errorMessage);
        }

        public DownloadSourceMeasurement ToMeasurement()
        {
            return new DownloadSourceMeasurement(
                Source,
                IsSuccess,
                BytesRead,
                BytesPerSecond,
                TimeToFirstByte,
                EstimatedDownloadTime,
                ErrorMessage);
        }
    }
}
