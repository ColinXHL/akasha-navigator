using System;
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
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogService _logService;
    private readonly WebView2HealthMonitor _healthMonitor;
    private int _queuedCount = 0;
    private int _totalExecuted = 0;
    private int _totalFailed = 0;
    private int _totalRejected = 0;

    private const int MaxQueueLength = 10;
    private const int DefaultTimeoutMs = 5000;

    public ScriptExecutionQueue(ILogService logService)
    {
        _logService = logService;
        _healthMonitor = new WebView2HealthMonitor(logService);
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
        CancellationToken cancellationToken = default)
    {
        // 检查队列长度，防止堆积过多
        if (_queuedCount >= MaxQueueLength)
        {
            Interlocked.Increment(ref _totalRejected);
            _logService.Warn(nameof(ScriptExecutionQueue),
                "Script execution rejected: queue is full (QueuedCount={QueuedCount}, Script={ScriptName})",
                _queuedCount, scriptName);
            return null;
        }

        Interlocked.Increment(ref _queuedCount);

        try
        {
            // 等待获取信号量（确保串行执行）
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
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
                    _healthMonitor.RecordSuccess();
                    _logService.Debug(nameof(ScriptExecutionQueue),
                        "Script executed successfully (Script={ScriptName}, QueuedCount={QueuedCount})",
                        scriptName, _queuedCount - 1);
                    return result;
                }
                else
                {
                    // 超时
                    Interlocked.Increment(ref _totalFailed);
                    _healthMonitor.RecordFailure();
                    _logService.Warn(nameof(ScriptExecutionQueue),
                        "Script execution timeout (Script={ScriptName}, TimeoutMs={TimeoutMs})",
                        scriptName, timeoutMs);
                    return null;
                }
            }
            finally
            {
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
        }
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void ResetStatistics()
    {
        _totalExecuted = 0;
        _totalFailed = 0;
        _totalRejected = 0;
        _healthMonitor.Reset();
        _logService.Info(nameof(ScriptExecutionQueue), "Statistics reset");
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    public string GetStatisticsSummary()
    {
        return $"Executed={_totalExecuted}, Failed={_totalFailed}, Rejected={_totalRejected}, Queued={_queuedCount}, Health={_healthMonitor.GetHealthSummary()}";
    }

    /// <summary>
    /// 获取健康监控器
    /// </summary>
    public WebView2HealthMonitor HealthMonitor => _healthMonitor;
}
