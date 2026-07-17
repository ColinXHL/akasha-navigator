using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services;

/// <summary>
/// 按顺序执行应用关停阶段，并保证整个关停流程幂等。
/// </summary>
public sealed class ShutdownCoordinator
{
    private readonly ILogService _logService;
    private readonly object _syncRoot = new();
    private readonly List<ShutdownStage> _stages = new();
    private bool _shutdownStarted;

    public ShutdownCoordinator(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public bool IsShutdownStarted
    {
        get
        {
            lock (_syncRoot)
            {
                return _shutdownStarted;
            }
        }
    }

    /// <summary>
    /// 注册一个关停阶段。较小的顺序值会先执行。
    /// </summary>
    public void RegisterStage(string name, int order, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(action);

        lock (_syncRoot)
        {
            if (_shutdownStarted)
            {
                throw new InvalidOperationException("关停流程开始后不能再注册阶段。");
            }

            if (_stages.Any(stage => string.Equals(stage.Name, name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"关停阶段已注册：{name}");
            }

            _stages.Add(new ShutdownStage(name, order, action));
        }
    }

    /// <summary>
    /// 执行全部关停阶段。重复调用不会再次执行。
    /// </summary>
    public void Shutdown()
    {
        ShutdownStage[] stages;
        lock (_syncRoot)
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            stages = _stages.OrderBy(stage => stage.Order).ToArray();
        }

        var totalStopwatch = Stopwatch.StartNew();
        _logService.Info(nameof(ShutdownCoordinator), "开始统一关停，共 {StageCount} 个阶段", stages.Length);

        foreach (var stage in stages)
        {
            var stageStopwatch = Stopwatch.StartNew();
            try
            {
                stage.Action();
                stageStopwatch.Stop();
                _logService.Info(
                    nameof(ShutdownCoordinator), "关停阶段完成: {StageName}, 耗时 {ElapsedMilliseconds}ms",
                    stage.Name, stageStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stageStopwatch.Stop();
                _logService.Error(
                    nameof(ShutdownCoordinator), ex, "关停阶段失败: {StageName}, 耗时 {ElapsedMilliseconds}ms",
                    stage.Name, stageStopwatch.ElapsedMilliseconds);
            }
        }

        totalStopwatch.Stop();
        _logService.Info(
            nameof(ShutdownCoordinator), "统一关停完成，总耗时 {ElapsedMilliseconds}ms",
            totalStopwatch.ElapsedMilliseconds);
    }

    private sealed record ShutdownStage(string Name, int Order, Action Action);
}
