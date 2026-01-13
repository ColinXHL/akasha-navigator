using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 进程信息模型
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// 进程名称（不含 .exe 扩展名）
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// 窗口标题
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称（进程名 - 窗口标题）
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(WindowTitle) ? ProcessName : $"{ProcessName} - {WindowTitle}";
}

/// <summary>
/// 进程列表辅助类
/// 提供进程列表的解析、序列化和获取运行进程的功能
/// </summary>
public static class ProcessListHelper
{
    /// <summary>
    /// 清理进程名：移除逗号、换行符、控制字符等不合法字符，并 trim 空白
    /// </summary>
    /// <param name="processName">原始进程名</param>
    /// <returns>清理后的进程名</returns>
    private static string CleanProcessName(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return string.Empty;

        // 移除逗号、换行符、回车符和其他控制字符
        var cleaned =
            new string(processName.Where(c => c != ',' && c != '\n' && c != '\r' && !char.IsControl(c)).ToArray());

        return cleaned.Trim();
    }

    /// <summary>
    /// 解析逗号分隔的进程列表字符串
    /// </summary>
    /// <param name="value">逗号分隔的进程名字符串</param>
    /// <returns>进程名列表</returns>
    public static List<string> ParseProcessList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanProcessName)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// 将进程列表序列化为逗号分隔字符串
    /// </summary>
    /// <param name="processes">进程名列表</param>
    /// <returns>逗号分隔的字符串</returns>
    public static string SerializeProcessList(IEnumerable<string> processes)
    {
        if (processes == null)
            return string.Empty;

        var validProcesses = processes.Where(s => !string.IsNullOrWhiteSpace(s))
                                 .Select(CleanProcessName)
                                 .Where(s => !string.IsNullOrEmpty(s))
                                 .Distinct();

        return string.Join(", ", validProcesses);
    }

    /// <summary>
    /// 获取当前运行的有窗口的进程列表
    /// </summary>
    /// <returns>进程信息列表</returns>
    public static List<ProcessInfo> GetRunningProcesses()
    {
        var result = new List<ProcessInfo>();

        try
        {
            var processes =
                Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)).OrderBy(p => p.ProcessName);

            foreach (var process in processes)
            {
                try
                {
                    result.Add(
                        new ProcessInfo { ProcessName = process.ProcessName, WindowTitle = process.MainWindowTitle });
                }
                catch
                {
                    // 忽略无法访问的进程
                }
            }
        }
        catch
        {
            // 获取进程列表失败时返回空列表
        }

        return result;
    }
}
}
