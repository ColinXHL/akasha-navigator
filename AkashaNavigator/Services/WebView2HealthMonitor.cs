using System;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace AkashaNavigator.Services;

/// <summary>
/// WebView2 健康监控服务
/// 监控 WebView2 的健康状态，检测潜在的崩溃风险
/// </summary>
public class WebView2HealthMonitor
{
    private readonly ILogService _logService;
    
    private int _consecutiveFailures = 0;
    private DateTime _lastSuccessTime = DateTime.Now;
    private DateTime _lastFailureTime = DateTime.MinValue;
    
    private const int FailureThreshold = 5;
    private const int HealthCheckTimeoutMs = 2000;

    public WebView2HealthMonitor(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// 连续失败次数
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// 上次成功时间
    /// </summary>
    public DateTime LastSuccessTime => _lastSuccessTime;

    /// <summary>
    /// 上次失败时间
    /// </summary>
    public DateTime LastFailureTime => _lastFailureTime;

    /// <summary>
    /// 是否需要恢复（连续失败次数超过阈值）
    /// </summary>
    public bool NeedsRecovery => _consecutiveFailures >= FailureThreshold;

    /// <summary>
    /// 执行健康检查
    /// </summary>
    /// <param name="webView">WebView2 实例</param>
    /// <returns>是否健康</returns>
    public async Task<bool> CheckHealthAsync(CoreWebView2 webView)
    {
        try
        {
            // 执行简单的脚本测试 WebView2 是否响应
            const string script = "(function() { return 'ok'; })();";
            
            var task = webView.ExecuteScriptAsync(script);
            var completedTask = await Task.WhenAny(task, Task.Delay(HealthCheckTimeoutMs));

            if (completedTask == task)
            {
                var result = await task;
                if (!string.IsNullOrEmpty(result))
                {
                    RecordSuccess();
                    return true;
                }
            }
            else
            {
                _logService.Warn(nameof(WebView2HealthMonitor), 
                    "Health check timeout ({TimeoutMs}ms)", HealthCheckTimeoutMs);
            }

            RecordFailure();
            return false;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(WebView2HealthMonitor), ex, "Health check failed");
            RecordFailure();
            return false;
        }
    }

    /// <summary>
    /// 记录成功
    /// </summary>
    public void RecordSuccess()
    {
        if (_consecutiveFailures > 0)
        {
            _logService.Info(nameof(WebView2HealthMonitor), 
                "WebView2 recovered (ConsecutiveFailures={ConsecutiveFailures})", _consecutiveFailures);
        }
        
        _consecutiveFailures = 0;
        _lastSuccessTime = DateTime.Now;
    }

    /// <summary>
    /// 记录失败
    /// </summary>
    public void RecordFailure()
    {
        _consecutiveFailures++;
        _lastFailureTime = DateTime.Now;
        
        _logService.Warn(nameof(WebView2HealthMonitor), 
            "WebView2 failure recorded (ConsecutiveFailures={ConsecutiveFailures}, NeedsRecovery={NeedsRecovery})",
            _consecutiveFailures, NeedsRecovery);

        if (NeedsRecovery)
        {
            _logService.Error(nameof(WebView2HealthMonitor), 
                "WebView2 needs recovery! (ConsecutiveFailures={ConsecutiveFailures})", _consecutiveFailures);
        }
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void Reset()
    {
        _consecutiveFailures = 0;
        _lastSuccessTime = DateTime.Now;
        _lastFailureTime = DateTime.MinValue;
        _logService.Info(nameof(WebView2HealthMonitor), "Health monitor reset");
    }

    /// <summary>
    /// 获取健康状态摘要
    /// </summary>
    public string GetHealthSummary()
    {
        var timeSinceLastSuccess = DateTime.Now - _lastSuccessTime;
        return $"ConsecutiveFailures={_consecutiveFailures}, " +
               $"TimeSinceLastSuccess={timeSinceLastSuccess.TotalSeconds:F1}s, " +
               $"NeedsRecovery={NeedsRecovery}";
    }
}
