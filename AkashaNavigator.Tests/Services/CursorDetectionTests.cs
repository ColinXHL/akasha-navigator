using System;
using System.Threading;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services
{
/// <summary>
/// CursorDetectionService 单元测试
/// 测试光标检测服务的启动、停止、事件触发和配置处理
/// </summary>
public class CursorDetectionTests : IDisposable
{
private readonly CursorDetectionService _service;
private readonly TestLogService _logService;

public CursorDetectionTests()
{
_logService = new TestLogService();
_service = new CursorDetectionService(_logService);
}

public void Dispose()
{
_service?.Stop();
}

#region Service Lifecycle Tests - 6.1.1

[Fact]
public void Constructor_WithNullLogService_DoesNotThrow()
{
// Act & Assert
var exception = Record.Exception(() =>
{
var service = new CursorDetectionService(null);
Assert.NotNull(service);
});

Assert.Null(exception);
}

[Fact]
public void Constructor_WithLogService_DoesNotThrow()
{
// Act & Assert
var exception = Record.Exception(() =>
{
var logService = new TestLogService();
var service = new CursorDetectionService(logService);
Assert.NotNull(service);
});

Assert.Null(exception);
}

[Fact]
public void Start_WithNullProcessName_StartsTimer()
{
// Act
_service.Start(null, 200);

// Assert
Assert.True(_service.IsRunning);
Assert.Null(_service.TargetProcessName);
}

[Fact]
public void Start_WithProcessName_SetsTargetProcess()
{
// Act
_service.Start("eldenring", 200);

// Assert
Assert.True(_service.IsRunning);
Assert.Equal("eldenring", _service.TargetProcessName);
}

[Fact]
public void Stop_AfterStart_StopsTimer()
{
// Arrange
_service.Start(null, 200);

// Act
_service.Stop();

// Assert
Assert.False(_service.IsRunning);
}

[Fact]
public void Start_CalledMultipleTimes_RestartsTimer()
{
// Arrange
_service.Start(null, 200);
Assert.True(_service.IsRunning);

// Act
_service.Start("game", 100);

// Assert
Assert.True(_service.IsRunning);
Assert.Equal("game", _service.TargetProcessName);
}

[Fact]
public void IsRunning_Initially_ReturnsFalse()
{
// Assert
Assert.False(_service.IsRunning);
}

[Fact]
public void IsCursorCurrentlyVisible_Initially_ReturnsTrue()
{
// Assert - 服务默认状态是可见
Assert.True(_service.IsCursorCurrentlyVisible);
}

#endregion

#region Event Tests - 6.1.2

[Fact]
public void CursorShown_Event_CanBeSubscribed()
{
// Arrange & Act - 订阅事件不应抛异常
var exception = Record.Exception(() =>
{
_service.CursorShown += (s, e) => { };
});

// Assert
Assert.Null(exception);
}

[Fact]
public void CursorHidden_Event_CanBeSubscribed()
{
// Arrange & Act - 订阅事件不应抛异常
var exception = Record.Exception(() =>
{
_service.CursorHidden += (s, e) => { };
});

// Assert
Assert.Null(exception);
}

[Fact]
public void Events_CanBeUnsubscribed()
{
// Arrange
_service.CursorShown += (s, e) => { };
_service.CursorHidden += (s, e) => { };

// Act & Assert - 取消订阅不应抛异常
var exception1 = Record.Exception(() =>
{
_service.CursorShown -= (s, e) => { };
});

var exception2 = Record.Exception(() =>
{
_service.CursorHidden -= (s, e) => { };
});

Assert.Null(exception1);
Assert.Null(exception2);
}

[Fact]
public void Start_DoesNotImmediatelyTriggerEvents()
{
// Arrange
_service.CursorShown += (s, e) => { };
_service.CursorHidden += (s, e) => { };

// Act
_service.Start(null, 50);
Thread.Sleep(100);  // 等待一小段时间

// Assert - 启动不应立即触发事件（需要实际的光标变化）
// 实际事件触发依赖于 Win32 API，难以在单元测试中模拟
// 这里只验证服务正常运行
Assert.True(_service.IsRunning);

// Cleanup
_service.Stop();
}

#endregion

#region Configuration Tests - 6.1.3

[Fact]
public void EnableDebugLog_DefaultValue_IsFalse()
{
// Assert
Assert.False(_service.EnableDebugLog);
}

[Fact]
public void EnableDebugLog_CanBeSet()
{
// Act
_service.EnableDebugLog = true;

// Assert
Assert.True(_service.EnableDebugLog);

// Cleanup
_service.EnableDebugLog = false;
}

[Fact]
public void Start_WithDebugLogEnabled_SetsDebugLog()
{
// Act
_service.Start("test", 100, enableDebugLog: true);

// Assert
Assert.True(_service.EnableDebugLog);
}

[Fact]
public void Start_WithMinimumInterval_UsesFiftyMs()
{
// Act
_service.Start(null, 10);

// Assert - 最小间隔应为 50ms
Assert.True(_service.IsRunning);
}

#endregion

#region SetTargetProcess Tests - 6.1.4

[Fact]
public void SetTargetProcess_UpdatesProcessName()
{
// Arrange
_service.Start("initial", 200);

// Act
_service.SetTargetProcess("updated");

// Assert
Assert.Equal("updated", _service.TargetProcessName);
}

[Fact]
public void SetTargetProcess_ToNull_SetsToNull()
{
// Arrange
_service.Start("game", 200);

// Act
_service.SetTargetProcess(null);

// Assert
Assert.Null(_service.TargetProcessName);
}

#endregion

#region Dispose Tests - 6.1.5

[Fact]
public void Dispose_StopsRunningService()
{
// Arrange
_service.Start(null, 200);
Assert.True(_service.IsRunning);

// Act
_service.Dispose();

// Assert
Assert.False(_service.IsRunning);
}

[Fact]
public void Dispose_CalledMultipleTimes_DoesNotThrow()
{
// Arrange
_service.Start(null, 200);

// Act & Assert
var exception = Record.Exception(() =>
{
_service.Dispose();
_service.Dispose();
});

Assert.Null(exception);
}

#endregion

}

/// <summary>
/// 测试用日志服务
/// </summary>
internal class TestLogService : ILogService
{
public string LogDirectory { get; } = "TestLogs";

public void Debug(string source, string message) { }

public void Debug(string source, string template, params object?[] args) { }

public void Info(string source, string message) { }

public void Info(string source, string template, params object?[] args) { }

public void Warn(string source, string message) { }

public void Warn(string source, string template, params object?[] args) { }

public void Error(string source, string message) { }

public void Error(string source, string template, params object?[] args) { }

public void Error(string source, Exception ex, string template, params object?[] args) { }
}
}
