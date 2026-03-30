using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Microsoft.Web.WebView2.Core;

namespace AkashaNavigator.Helpers;

/// <summary>
/// WebView2 脚本执行队列，确保脚本串行执行，防止并发导致的资源耗尽
/// </summary>
public class ScriptExecutionQueue
{
    private sealed class ScriptRequestInfo
    {
        public long RequestId { get; init; }
        public string ScriptName { get; init; } = "UnnamedScript";
        public ScriptExecutionPriority Priority { get; init; }
        public string? CoalesceKey { get; init; }
        public DateTime EnqueuedAtUtc { get; init; }
        public DateTime? ExecutionStartedAtUtc { get; set; }
    }

    public enum ScriptExecutionPriority
    {
        Low,
        Normal,
        High
    }

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogService _logService;
    private readonly WebView2HealthMonitor _healthMonitor;
    private readonly ConcurrentDictionary<string, long> _coalesceVersions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _activeCoalesceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, ScriptRequestInfo> _activeRequests = new();
    private readonly object _queueResetLock = new();
    private CancellationTokenSource _queueResetCts = new();
    private readonly string _queueResetLogFilePath;
    private readonly string _queueSnapshotLogFilePath;
    private int _queuedCount = 0;
    private int _totalExecuted = 0;
    private int _totalFailed = 0;
    private int _totalRejected = 0;
    private int _totalCoalesced = 0;
    private long _totalQueueWaitMs = 0;
    private long _totalExecutionMs = 0;
    private long _coalesceVersionCounter = 0;
    private long _requestIdCounter = 0;
    private long _currentlyExecutingRequestId = 0;
    private int _queueResetCount = 0;
    private DateTime _lastQueueResetAtUtc = DateTime.MinValue;

    private const int MaxQueueLength = 10;
    private const int LowPrioritySoftLimit = 4;
    private const int HighPriorityReserveSlots = 2;
    private const int DefaultTimeoutMs = 5000;
    private const int HighPriorityWaitWarnMs = 1000;
    private static readonly TimeSpan MinQueueResetInterval = TimeSpan.FromSeconds(2);

    public ScriptExecutionQueue(ILogService logService)
    {
        _logService = logService;
        _healthMonitor = new WebView2HealthMonitor(logService);

        var diagnosticsDirectory = Path.Combine(AppPaths.DataDirectory, "Diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        _queueResetLogFilePath = Path.Combine(diagnosticsDirectory, "script-queue-reset.log");
        _queueSnapshotLogFilePath = Path.Combine(diagnosticsDirectory, "script-queue-snapshot.log");
    }

    /// <summary>
    /// 当前队列中等待执行的脚本数量
    /// </summary>
    public int QueuedCount => _queuedCount;

    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalExecuted => _totalExecuted;

    /// <summary>
    /// 总失败次数
    /// </summary>
    public int TotalFailed => _totalFailed;

    /// <summary>
    /// 总拒绝次数（队列已满）
    /// </summary>
    public int TotalRejected => _totalRejected;

    /// <summary>
    /// 总合并丢弃次数（同类请求被新请求覆盖）
    /// </summary>
    public int TotalCoalesced => _totalCoalesced;

    /// <summary>
    /// 当前是否处于队列拥塞状态
    /// </summary>
    public bool IsBacklogged(int threshold = 2)
    {
        if (threshold < 1)
            threshold = 1;

        return Volatile.Read(ref _queuedCount) >= threshold;
    }

    /// <summary>
    /// 执行 WebView2 脚本（串行化，带超时和队列长度限制）
    /// </summary>
    /// <param name="webView">WebView2 实例</param>
    /// <param name="script">要执行的 JavaScript 代码</param>
    /// <param name="scriptName">脚本名称（用于日志）</param>
    /// <param name="timeoutMs">超时时间（毫秒），默认 5000ms</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>脚本执行结果，失败时返回 null</returns>
    public async Task<string?> ExecuteAsync(
        CoreWebView2 webView,
        string script,
        string scriptName = "UnnamedScript",
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default,
        ScriptExecutionPriority priority = ScriptExecutionPriority.Normal,
        string? coalesceKey = null)
    {
        var currentQueuedCount = Volatile.Read(ref _queuedCount);

        // 低优先级任务在轻度拥塞时即主动让路，优先保障交互动作
        if (priority == ScriptExecutionPriority.Low && currentQueuedCount >= LowPrioritySoftLimit)
        {
            Interlocked.Increment(ref _totalRejected);
            _logService.Debug(nameof(ScriptExecutionQueue),
                              "Low priority script skipped due to queue pressure (QueuedCount={QueuedCount}, Script={ScriptName})",
                              currentQueuedCount, scriptName);
            return null;
        }

        // 为高优先级操作保留容量，避免队列被后台任务完全占满
        if (priority != ScriptExecutionPriority.High && currentQueuedCount >= MaxQueueLength - HighPriorityReserveSlots)
        {
            Interlocked.Increment(ref _totalRejected);
            _logService.Debug(nameof(ScriptExecutionQueue),
                              "Non-high priority script skipped to preserve interactive capacity (Priority={Priority}, QueuedCount={QueuedCount}, Script={ScriptName}, Snapshot={Snapshot})",
                              priority, currentQueuedCount, scriptName, BuildQueueSnapshot());
            return null;
        }

        // 检查队列长度，防止堆积过多
        if (currentQueuedCount >= MaxQueueLength)
        {
            Interlocked.Increment(ref _totalRejected);
            var snapshot = BuildQueueSnapshot();
            _logService.Warn(nameof(ScriptExecutionQueue),
                "Script execution rejected: queue is full (QueuedCount={QueuedCount}, Script={ScriptName}, Priority={Priority}, Snapshot={Snapshot})",
                currentQueuedCount, scriptName, priority, snapshot);

            TryResetQueueOnOverflow(scriptName, priority, snapshot);
            return null;
        }

        long myCoalesceVersion = 0;
        if (!string.IsNullOrWhiteSpace(coalesceKey))
        {
            myCoalesceVersion = Interlocked.Increment(ref _coalesceVersionCounter);
            _coalesceVersions[coalesceKey] = myCoalesceVersion;

            if (!_activeCoalesceKeys.TryAdd(coalesceKey, 0))
            {
                Interlocked.Increment(ref _totalCoalesced);
                _logService.Debug(nameof(ScriptExecutionQueue),
                                  "Script coalesced before enqueue (Script={ScriptName}, CoalesceKey={CoalesceKey})",
                                  scriptName, coalesceKey);
                return null;
            }
        }

        var requestId = Interlocked.Increment(ref _requestIdCounter);
        _activeRequests[requestId] = new ScriptRequestInfo {
            RequestId = requestId,
            ScriptName = scriptName,
            Priority = priority,
            CoalesceKey = coalesceKey,
            EnqueuedAtUtc = DateTime.UtcNow
        };

        Interlocked.Increment(ref _queuedCount);
        var queuedAt = Stopwatch.GetTimestamp();

        try
        {
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetQueueResetToken());

            // 等待获取信号量（确保串行执行）
            await _semaphore.WaitAsync(waitCts.Token);
            var waitMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds;
            Interlocked.Add(ref _totalQueueWaitMs, (long)waitMs);

            if (_activeRequests.TryGetValue(requestId, out var requestInfo))
            {
                requestInfo.ExecutionStartedAtUtc = DateTime.UtcNow;
            }

            Volatile.Write(ref _currentlyExecutingRequestId, requestId);

            if (priority == ScriptExecutionPriority.High && waitMs >= HighPriorityWaitWarnMs)
            {
                _logService.Warn(nameof(ScriptExecutionQueue),
                                 "High-priority script waited too long (Script={ScriptName}, WaitMs={WaitMs:F0}, Snapshot={Snapshot})",
                                 scriptName, waitMs, BuildQueueSnapshot());
            }

            if (!string.IsNullOrWhiteSpace(coalesceKey) &&
                _coalesceVersions.TryGetValue(coalesceKey, out var latestVersion) &&
                latestVersion != myCoalesceVersion)
            {
                Interlocked.Increment(ref _totalCoalesced);
                _logService.Debug(nameof(ScriptExecutionQueue),
                                  "Script coalesced before execution (Script={ScriptName}, CoalesceKey={CoalesceKey})",
                                  scriptName, coalesceKey);
                return null;
            }

            try
            {
                var executeStartedAt = Stopwatch.GetTimestamp();

                // 使用超时控制执行
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var executeTask = webView.ExecuteScriptAsync(script);
                var completedTask = await Task.WhenAny(executeTask, Task.Delay(timeoutMs, linkedCts.Token));

                if (completedTask == executeTask)
                {
                    // 脚本执行完成
                    var result = await executeTask;
                    Interlocked.Increment(ref _totalExecuted);
                    var executionMs = Stopwatch.GetElapsedTime(executeStartedAt).TotalMilliseconds;
                    Interlocked.Add(ref _totalExecutionMs, (long)executionMs);
                    _healthMonitor.RecordSuccess();
                    _logService.Debug(nameof(ScriptExecutionQueue),
                        "Script executed successfully (Script={ScriptName}, QueuedCount={QueuedCount}, WaitMs={WaitMs:F0}, ExecuteMs={ExecuteMs:F0})",
                        scriptName, _queuedCount - 1, waitMs, executionMs);
                    return result;
                }
                else
                {
                    // 超时
                    Interlocked.Increment(ref _totalFailed);
                    _healthMonitor.RecordFailure();
                    _logService.Warn(nameof(ScriptExecutionQueue),
                        "Script execution timeout (Script={ScriptName}, TimeoutMs={TimeoutMs}, Snapshot={Snapshot})",
                        scriptName, timeoutMs, BuildQueueSnapshot());
                    return null;
                }
            }
            finally
            {
                Interlocked.CompareExchange(ref _currentlyExecutingRequestId, 0, requestId);
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _totalFailed);
            _healthMonitor.RecordFailure();
            _logService.Debug(nameof(ScriptExecutionQueue),
                "Script execution cancelled (Script={ScriptName})",
                scriptName);
            return null;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalFailed);
            _healthMonitor.RecordFailure();
            _logService.Error(nameof(ScriptExecutionQueue), ex,
                "Script execution failed (Script={ScriptName})",
                scriptName);
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _queuedCount);
            _activeRequests.TryRemove(requestId, out _);
            if (!string.IsNullOrWhiteSpace(coalesceKey))
            {
                _activeCoalesceKeys.TryRemove(coalesceKey, out _);
            }
        }
    }

    private string BuildQueueSnapshot()
    {
        var nowUtc = DateTime.UtcNow;
        var requests = _activeRequests.Values.ToArray();

        var waiting = requests.Where(x => x.ExecutionStartedAtUtc == null).ToArray();
        var waitingCount = waiting.Length;
        var oldestWaitingMs = waitingCount > 0
            ? (int)waiting.Max(x => (nowUtc - x.EnqueuedAtUtc).TotalMilliseconds)
            : 0;

        var topWaiting = waiting.GroupBy(x => x.ScriptName, StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(g => g.Count())
                                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                                .Take(3)
                                .Select(g => $"{g.Key}x{g.Count()}")
                                .ToArray();

        var topWaitingText = topWaiting.Length > 0 ? string.Join(", ", topWaiting) : "none";

        var executingId = Volatile.Read(ref _currentlyExecutingRequestId);
        string executingText;
        if (executingId != 0 && _activeRequests.TryGetValue(executingId, out var executingInfo))
        {
            var executeElapsedMs = executingInfo.ExecutionStartedAtUtc.HasValue
                ? (int)(nowUtc - executingInfo.ExecutionStartedAtUtc.Value).TotalMilliseconds
                : 0;
            executingText = $"{executingInfo.ScriptName}({executeElapsedMs}ms)";
        }
        else
        {
            executingText = "none";
        }

        return $"Waiting={waitingCount}, OldestWaitMs={oldestWaitingMs}, Executing={executingText}, TopWaiting={topWaitingText}";
    }

    private string BuildQueueSnapshotDetailed()
    {
        var nowUtc = DateTime.UtcNow;
        var requests = _activeRequests.Values.ToArray();
        var waiting = requests.Where(x => x.ExecutionStartedAtUtc == null).ToArray();

        var byScript = waiting.GroupBy(x => x.ScriptName, StringComparer.OrdinalIgnoreCase)
                              .OrderByDescending(g => g.Count())
                              .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                              .Select(g => $"{g.Key}={g.Count()}")
                              .ToArray();

        var byPriority = waiting.GroupBy(x => x.Priority)
                                .OrderByDescending(g => g.Count())
                                .Select(g => $"{g.Key}={g.Count()}")
                                .ToArray();

        var oldestWaitMs = waiting.Length > 0
            ? (int)waiting.Max(x => (nowUtc - x.EnqueuedAtUtc).TotalMilliseconds)
            : 0;

        return
            $"Queued={Volatile.Read(ref _queuedCount)}, Waiting={waiting.Length}, OldestWaitMs={oldestWaitMs}, ByPriority=[{string.Join(", ", byPriority)}], ByScript=[{string.Join(", ", byScript)}]";
    }

    private CancellationToken GetQueueResetToken()
    {
        lock (_queueResetLock)
        {
            return _queueResetCts.Token;
        }
    }

    private void TryResetQueueOnOverflow(string scriptName, ScriptExecutionPriority priority, string snapshot)
    {
        DateTime resetAtUtc;
        int resetCount;

        lock (_queueResetLock)
        {
            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastQueueResetAtUtc < MinQueueResetInterval)
            {
                return;
            }

            _lastQueueResetAtUtc = nowUtc;
            resetCount = Interlocked.Increment(ref _queueResetCount);
            resetAtUtc = nowUtc;

            var oldCts = _queueResetCts;
            _queueResetCts = new CancellationTokenSource();

            _activeCoalesceKeys.Clear();
            _coalesceVersions.Clear();

            try
            {
                oldCts.Cancel();
            }
            catch
            {
                // 忽略取消过程中的异常
            }
            finally
            {
                oldCts.Dispose();
            }
        }

        var detailed = BuildQueueSnapshotDetailed();
        _logService.Warn(nameof(ScriptExecutionQueue),
                         "Queue overflow workaround triggered reset (ResetCount={ResetCount}, Script={ScriptName}, Priority={Priority}, Snapshot={Snapshot})",
                         resetCount, scriptName, priority, snapshot);

        WriteQueueDiagnostics(resetAtUtc, resetCount, scriptName, priority, snapshot, detailed);
    }

    private void WriteQueueDiagnostics(DateTime resetAtUtc, int resetCount, string scriptName,
                                       ScriptExecutionPriority priority, string snapshot, string detailed)
    {
        try
        {
            var timestamp = resetAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            var resetLine =
                $"[{timestamp}] ResetCount={resetCount}, TriggerScript={scriptName}, Priority={priority}, Snapshot={snapshot}{Environment.NewLine}";
            File.AppendAllText(_queueResetLogFilePath, resetLine);

            var snapshotLine =
                $"[{timestamp}] ResetCount={resetCount}, Detail={detailed}{Environment.NewLine}";
            File.AppendAllText(_queueSnapshotLogFilePath, snapshotLine);
        }
        catch (Exception ex)
        {
            _logService.Debug(nameof(ScriptExecutionQueue),
                              "Failed to write queue diagnostics files: {ErrorMessage}",
                              ex.Message);
        }
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalExecuted, 0);
        Interlocked.Exchange(ref _totalFailed, 0);
        Interlocked.Exchange(ref _totalRejected, 0);
        Interlocked.Exchange(ref _totalCoalesced, 0);
        Interlocked.Exchange(ref _totalQueueWaitMs, 0);
        Interlocked.Exchange(ref _totalExecutionMs, 0);
        _healthMonitor.Reset();
        _logService.Info(nameof(ScriptExecutionQueue), "Statistics reset");
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    public string GetStatisticsSummary()
    {
        var executed = Volatile.Read(ref _totalExecuted);
        var failed = Volatile.Read(ref _totalFailed);
        var rejected = Volatile.Read(ref _totalRejected);
        var coalesced = Volatile.Read(ref _totalCoalesced);
        var queued = Volatile.Read(ref _queuedCount);
        var totalWait = Volatile.Read(ref _totalQueueWaitMs);
        var totalExecution = Volatile.Read(ref _totalExecutionMs);

        var averageWaitMs = executed > 0 ? totalWait / (double)executed : 0;
        var averageExecutionMs = executed > 0 ? totalExecution / (double)executed : 0;

        return $"Executed={executed}, Failed={failed}, Rejected={rejected}, Coalesced={coalesced}, Queued={queued}, AvgWaitMs={averageWaitMs:F1}, AvgExecMs={averageExecutionMs:F1}, Health={_healthMonitor.GetHealthSummary()}";
    }

    /// <summary>
    /// 获取健康监控器
    /// </summary>
    public WebView2HealthMonitor HealthMonitor => _healthMonitor;
}
