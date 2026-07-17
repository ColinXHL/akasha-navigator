using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
/// 使用插件包前 512 KiB 测量下载源，并按预计完整下载时间排序。
/// </summary>
public sealed class DownloadSourceSelector : IDownloadSourceSelector
{
    private const int ProbeByteCount = 512 * 1024;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);

    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ConcurrentDictionary<string, CachedSelection> _cache = new();
    private readonly ConcurrentDictionary<string, string> _lastSelectedSource = new();

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

        var sources = package.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Id) &&
                             Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
                             uri.Scheme == Uri.UriSchemeHttps)
            .ToList();
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

        var cacheKey = CreateCacheKey(package, sources);
        if (_cache.TryGetValue(cacheKey, out var cached) &&
            DateTimeOffset.UtcNow - cached.CreatedAt < CacheLifetime)
        {
            return Result<IReadOnlyList<DownloadSourceInfo>>.Success(
                OrderByIds(sources, cached.SourceIds));
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
            return Result<IReadOnlyList<DownloadSourceInfo>>.Failure(
                Error.Network(
                    "PLUGIN_SOURCE_PROBE_FAILED",
                    $"所有插件下载源测速失败：{errors}"));
        }

        ApplyHysteresis(cacheKey, successful);

        var successfulIds = successful.Select(result => result.Source.Id).ToList();
        var failedIds = probeResults
            .Where(result => !result.IsSuccess)
            .Select(result => result.Source.Id);
        var orderedIds = successfulIds.Concat(failedIds).ToList();
        _cache[cacheKey] = new CachedSelection(DateTimeOffset.UtcNow, orderedIds);
        _lastSelectedSource[cacheKey] = orderedIds[0];

        return Result<IReadOnlyList<DownloadSourceInfo>>.Success(
            OrderByIds(sources, orderedIds));
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private async Task<ProbeResult> ProbeAsync(
        DownloadSourceInfo source,
        long packageSize,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ProbeTimeout);
        var stopwatch = Stopwatch.StartNew();

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
            var bytesRead = 0;
            TimeSpan? firstByteTime = null;
            while (bytesRead < ProbeByteCount)
            {
                var count = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, ProbeByteCount - bytesRead)),
                    timeoutSource.Token);
                if (count == 0)
                {
                    break;
                }

                firstByteTime ??= stopwatch.Elapsed;
                bytesRead += count;
            }

            stopwatch.Stop();
            if (bytesRead == 0 || firstByteTime == null)
            {
                return ProbeResult.Failure(source, "未读取到数据");
            }

            var transferDuration = stopwatch.Elapsed - firstByteTime.Value;
            var transferSeconds = Math.Max(transferDuration.TotalSeconds, 0.001);
            var bytesPerSecond = bytesRead / transferSeconds;
            var estimatedSeconds =
                firstByteTime.Value.TotalSeconds +
                Math.Max(packageSize, bytesRead) / bytesPerSecond;
            return ProbeResult.Success(
                source,
                TimeSpan.FromSeconds(estimatedSeconds));
        }
        catch (OperationCanceledException)
        {
            return ProbeResult.Failure(
                source,
                cancellationToken.IsCancellationRequested ? "已取消" : "测速超时");
        }
        catch (Exception ex)
        {
            return ProbeResult.Failure(source, ex.Message);
        }
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

    private sealed record ProbeResult(
        DownloadSourceInfo Source,
        bool IsSuccess,
        TimeSpan EstimatedDownloadTime,
        string ErrorMessage)
    {
        public static ProbeResult Success(
            DownloadSourceInfo source,
            TimeSpan estimatedDownloadTime)
        {
            return new ProbeResult(source, true, estimatedDownloadTime, string.Empty);
        }

        public static ProbeResult Failure(DownloadSourceInfo source, string errorMessage)
        {
            return new ProbeResult(source, false, TimeSpan.MaxValue, errorMessage);
        }
    }
}
