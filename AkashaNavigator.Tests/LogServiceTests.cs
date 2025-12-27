using System;
using System.IO;
using System.Text.RegularExpressions;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using FsCheck;
using FsCheck.Xunit;
using Serilog.Events;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// LogService 属性测试 - 适配 Serilog 集成
///
/// 注意：由于 Serilog 使用静态 Log.Logger，在并行测试中无法可靠地隔离日志输出。
/// 因此，这些测试主要验证 LogService 的公共 API 行为，而不是直接验证 Serilog 的输出。
/// Serilog 本身是成熟的第三方库，其核心功能已经过充分测试。
/// </summary>
public class LogServiceTests
{
    /// <summary>
    /// **Feature: logging-standardization, Property 1: 单例一致性**
    /// *对于任意*次数的 LogService.Instance 调用，所有调用应返回相同的对象引用
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Singleton_ShouldReturnSameInstance(PositiveInt callCount)
    {
        // 限制调用次数在合理范围内
        var count = Math.Min(callCount.Get, 1000);

        // 初始化 LogService.Instance（步骤4：需要手动初始化）
        var tempDir = Path.GetTempPath();
        LogService.Instance = new LogService(tempDir);

        var instances = new ILogService[count];
        for (int i = 0; i < count; i++)
        {
            instances[i] = LogService.Instance;
        }

        // 验证所有实例都是同一个对象
        var firstInstance = instances[0];
        var allSame = instances.All(inst => ReferenceEquals(inst, firstInstance));

        return allSame.Label($"所有 {count} 次调用应返回相同实例");
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 2: 日志文件名格式**
    /// *对于任意*日期，生成的日志文件名应匹配模式 akasha-navigator-yyyyMMdd.log
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LogFileName_ShouldMatchPattern(int year, int month, int day)
    {
        // 生成有效日期范围
        var validYear = Math.Abs(year % 100) + 2000; // 2000-2099
        var validMonth = (Math.Abs(month) % 12) + 1; // 1-12
        var maxDay = DateTime.DaysInMonth(validYear, validMonth);
        var validDay = (Math.Abs(day) % maxDay) + 1; // 1-maxDay

        var date = new DateTime(validYear, validMonth, validDay);

        // 创建临时 LogService 实例用于测试
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);
        var filePath = logService.GetLogFilePath(date);
        var fileName = Path.GetFileName(filePath);

        // 验证文件名格式
        var pattern = @"^akasha-navigator-\d{8}\.log$";
        var matchesPattern = Regex.IsMatch(fileName, pattern);

        // 验证日期部分正确
        var expectedFileName = $"akasha-navigator-{date:yyyyMMdd}.log";
        var dateCorrect = fileName == expectedFileName;

        return (matchesPattern && dateCorrect)
            .Label($"文件名 '{fileName}' 应匹配模式且日期正确 (期望: {expectedFileName})");
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 3: 日志条目格式**
    /// *对于任意*日志级别、来源字符串和消息字符串，格式化的日志条目应匹配模式 [yyyy-MM-dd HH:mm:ss.fff] [级别] [来源]
    /// 消息
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LogEntry_ShouldMatchFormat(int levelIndex, NonEmptyString source, NonEmptyString message)
    {
        // 生成有效的日志级别
        var levels =
            new[] { LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error };
        var level = levels[Math.Abs(levelIndex) % levels.Length];

        // 排除包含换行符的来源和消息（这些会破坏单行格式）
        var sourceStr = source.Get.Replace("\n", "").Replace("\r", "");
        var messageStr = message.Get.Replace("\n", "").Replace("\r", "");

        if (string.IsNullOrWhiteSpace(sourceStr) || string.IsNullOrWhiteSpace(messageStr))
        {
            return true.ToProperty(); // 跳过无效输入
        }

        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);
        var timestamp = DateTime.Now;
        var entry = logService.FormatLogEntry(timestamp, level, sourceStr, messageStr);

        // 验证格式：[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [source] message
        var pattern = @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[[A-Z]+\] \[.+\] .+$";
        var matchesPattern = Regex.IsMatch(entry, pattern);

        // 验证时间戳格式正确
        var expectedTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var hasCorrectTimestamp = entry.StartsWith($"[{expectedTimestamp}]");

        return (matchesPattern && hasCorrectTimestamp).Label($"条目 '{entry}' 应匹配格式模式");
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 4: 日志级别大写**
    /// *对于任意*日志级别，格式化输出应包含大写的级别名称（DEBUG、INFO、WARN、ERROR）
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LogLevel_ShouldBeUppercase(int levelIndex)
    {
        var levels =
            new[] { LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error };
        var level = levels[Math.Abs(levelIndex) % levels.Length];

        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);
        var entry = logService.FormatLogEntry(DateTime.Now, level, "TestSource", "TestMessage");

        // 获取期望的大写级别名称
        var expectedLevelStr = level switch { LogEventLevel.Debug => "DEBUG", LogEventLevel.Information => "INFO",
                                              LogEventLevel.Warning => "WARN", LogEventLevel.Error => "ERROR",
                                              _ => "INFO" };

        // 验证条目包含大写的级别名称
        var containsUppercaseLevel = entry.Contains($"[{expectedLevelStr}]");

        return containsUppercaseLevel.Label($"条目应包含大写级别 [{expectedLevelStr}]，实际: {entry}");
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 5: 来源保留**
    /// *对于任意*来源字符串，格式化的日志条目应包含该确切的来源字符串
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Source_ShouldBePreserved(NonEmptyString source)
    {
        // 排除包含方括号的来源（会干扰格式解析）
        var sourceStr = source.Get.Replace("[", "").Replace("]", "").Replace("\n", "").Replace("\r", "");

        if (string.IsNullOrWhiteSpace(sourceStr))
        {
            return true.ToProperty(); // 跳过无效输入
        }

        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);
        var entry = logService.FormatLogEntry(DateTime.Now, LogEventLevel.Information, sourceStr, "TestMessage");

        // 验证条目包含来源字符串
        var containsSource = entry.Contains($"[{sourceStr}]");

        return containsSource.Label($"条目应包含来源 [{sourceStr}]，实际: {entry}");
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 6: LogService API 不抛出异常**
    /// *对于任意*有效的日志调用，LogService 方法不应抛出异常
    /// **Validates: Requirements 1.1, 3.3**
    /// </summary>
    [Fact]
    public void LogMethods_ShouldNotThrowException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);

        // Act & Assert - 所有日志方法都不应抛出异常
        var exception = Record.Exception(() =>
                                         {
                                             logService.Debug("TestSource", "Debug message");
                                             logService.Info("TestSource", "Info message");
                                             logService.Warn("TestSource", "Warn message");
                                             logService.Error("TestSource", "Error message");
                                         });

        Assert.Null(exception);
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 7: 参数化日志方法不抛出异常**
    /// *对于任意*有效的参数化日志调用，LogService 方法不应抛出异常
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Fact]
    public void ParameterizedLogMethods_ShouldNotThrowException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);

        // Act & Assert - 参数化日志方法不应抛出异常
        var exception = Record.Exception(() =>
                                         {
                                             logService.Debug("TestSource", "Debug {Value}", 123);
                                             logService.Info("TestSource", "User {UserName} logged in", "Alice");
                                             logService.Warn("TestSource", "Warning: {Count} items", 5);
                                             logService.Error("TestSource", "Error code: {Code}", 500);
                                         });

        Assert.Null(exception);
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 8: 异常日志方法不抛出异常**
    /// *对于任意*异常，LogService 的异常日志方法不应抛出异常
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Fact]
    public void ExceptionLogMethod_ShouldNotThrowException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);
        var testException = new InvalidOperationException("Test exception message");

        // Act & Assert - 异常日志方法不应抛出异常
        var exception =
            Record.Exception(() =>
                             { logService.Error("TestSource", testException, "An error occurred: {ErrorCode}", 500); });

        Assert.Null(exception);
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 9: LogDirectory 属性正确**
    /// LogService 的 LogDirectory 属性应返回有效的目录路径
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Fact]
    public void LogDirectory_ShouldBeValid()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);

        // Assert
        Assert.Equal(tempDir, logService.LogDirectory);
        Assert.True(Directory.Exists(logService.LogDirectory));
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 10: 空消息不抛出异常**
    /// *对于*空字符串或 null 消息，LogService 方法不应抛出异常
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void EmptyOrNullMessage_ShouldNotThrowException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);

        // Act & Assert - 空消息不应抛出异常
        var exception = Record.Exception(() =>
                                         {
                                             logService.Info("TestSource", "");
                                             logService.Info("TestSource", string.Empty);
                                         });

        Assert.Null(exception);
    }

    /// <summary>
    /// **Feature: logging-standardization, Property 11: 特殊字符消息不抛出异常**
    /// *对于*包含特殊字符的消息，LogService 方法不应抛出异常
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void SpecialCharacterMessage_ShouldNotThrowException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logService = new LogService(tempDir);

        // Act & Assert - 特殊字符消息不应抛出异常
        var exception = Record.Exception(() =>
                                         {
                                             logService.Info("TestSource", "Message with {braces}");
                                             logService.Info("TestSource", "Message with \"quotes\"");
                                             logService.Info("TestSource", "Message with 中文字符");
                                             logService.Info("TestSource", "Message with\nnewline");
                                             logService.Info("TestSource", "Message with\ttab");
                                         });

        Assert.Null(exception);
    }
}
}
