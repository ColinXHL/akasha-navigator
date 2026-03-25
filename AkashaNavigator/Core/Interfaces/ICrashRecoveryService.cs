using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// 崩溃恢复服务接口
/// 检测程序异常退出并提供恢复机制
/// </summary>
public interface ICrashRecoveryService
{
    /// <summary>
    /// 检测是否有崩溃标记（上次程序异常退出）
    /// </summary>
    /// <returns>是否检测到崩溃</returns>
    bool DetectCrash();

    /// <summary>
    /// 清理 WebView2 数据目录
    /// </summary>
    /// <returns>清理结果</returns>
    Result CleanWebView2Data();

    /// <summary>
    /// 标记程序正常启动
    /// </summary>
    void MarkStartup();

    /// <summary>
    /// 标记程序正常关闭
    /// </summary>
    void MarkShutdown();

    /// <summary>
    /// 获取崩溃锁文件路径
    /// </summary>
    string LockFilePath { get; }

    /// <summary>
    /// 获取 WebView2 数据目录路径
    /// </summary>
    string WebView2DataPath { get; }
}
