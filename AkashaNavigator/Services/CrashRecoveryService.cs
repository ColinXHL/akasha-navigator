using System;
using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.Services;

/// <summary>
/// 崩溃恢复服务实现
/// </summary>
public class CrashRecoveryService : ICrashRecoveryService
{
    private readonly ILogService _logService;
    private const string LockFileName = "app.lock";

    public CrashRecoveryService(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>
    /// 崩溃锁文件路径
    /// </summary>
    public string LockFilePath => Path.Combine(AppPaths.DataDirectory, LockFileName);

    /// <summary>
    /// WebView2 数据目录路径
    /// </summary>
    public string WebView2DataPath => AppPaths.WebView2DataDirectory;

    /// <summary>
    /// 检测是否有崩溃标记
    /// </summary>
    public bool DetectCrash()
    {
        try
        {
            if (File.Exists(LockFilePath))
            {
                _logService.Warn(nameof(CrashRecoveryService), 
                    "Crash detected: lock file exists at {LockFilePath}", LockFilePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, "Failed to detect crash");
            return false;
        }
    }

    /// <summary>
    /// 清理 WebView2 数据目录
    /// </summary>
    public Result CleanWebView2Data()
    {
        try
        {
            if (!Directory.Exists(WebView2DataPath))
            {
                _logService.Info(nameof(CrashRecoveryService), 
                    "WebView2 data directory does not exist: {Path}", WebView2DataPath);
                return Result.Success();
            }

            _logService.Info(nameof(CrashRecoveryService), 
                "Cleaning WebView2 data directory: {Path}", WebView2DataPath);

            // 删除目录及其所有内容
            Directory.Delete(WebView2DataPath, recursive: true);

            _logService.Info(nameof(CrashRecoveryService), 
                "WebView2 data directory cleaned successfully");

            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, 
                "Access denied when cleaning WebView2 data");
            return Result.Failure("无法清理 WebView2 数据：权限不足");
        }
        catch (IOException ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, 
                "IO error when cleaning WebView2 data");
            return Result.Failure($"无法清理 WebView2 数据：{ex.Message}");
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, 
                "Unexpected error when cleaning WebView2 data");
            return Result.Failure($"清理 WebView2 数据时发生错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 标记程序正常启动
    /// </summary>
    public void MarkStartup()
    {
        try
        {
            // 确保数据目录存在
            Directory.CreateDirectory(AppPaths.DataDirectory);

            // 创建锁文件
            File.WriteAllText(LockFilePath, DateTime.Now.ToString("O"));

            _logService.Info(nameof(CrashRecoveryService), 
                "Application startup marked: {LockFilePath}", LockFilePath);
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, 
                "Failed to mark application startup");
        }
    }

    /// <summary>
    /// 标记程序正常关闭
    /// </summary>
    public void MarkShutdown()
    {
        try
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
                _logService.Info(nameof(CrashRecoveryService), 
                    "Application shutdown marked: lock file deleted");
            }
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CrashRecoveryService), ex, 
                "Failed to mark application shutdown");
        }
    }
}
