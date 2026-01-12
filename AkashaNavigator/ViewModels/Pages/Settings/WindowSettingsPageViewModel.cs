using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 运行中的窗口进程信息
/// </summary>
public class RunningProcess
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrEmpty(WindowTitle) ? ProcessName : $"{ProcessName} - {WindowTitle}";
}

/// <summary>
/// 窗口设置页面 ViewModel
/// 包含：边缘吸附、吸附阈值、退出时提示记录、全局鼠标检测透明度自动调整
/// </summary>
public partial class WindowSettingsPageViewModel : ObservableObject
{
#region Win32 API

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

#endregion

    /// <summary>
    /// 是否启用边缘吸附（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableEdgeSnap;

    /// <summary>
    /// 吸附阈值（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _snapThreshold;

    /// <summary>
    /// 是否在退出时提示记录笔记（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _promptRecordOnExit;

#region 全局鼠标检测配置

    /// <summary>
    /// 是否启用全局鼠标检测（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _cursorDetectionEnabled;

    /// <summary>
    /// UI 模式下的最低透明度（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private double _cursorDetectionMinOpacity = 0.3;

    /// <summary>
    /// 检测间隔（毫秒，自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _cursorDetectionCheckIntervalMs = 200;

    /// <summary>
    /// 是否启用调试日志（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _cursorDetectionEnableDebugLog;

    /// <summary>
    /// 进程白名单（用于 UI 绑定）
    /// </summary>
    public ObservableCollection<string> ProcessWhitelist { get; } = new();

    /// <summary>
    /// 当前运行的窗口进程列表（用于下拉选择）
    /// </summary>
    public ObservableCollection<RunningProcess> RunningProcesses { get; } = new();

    /// <summary>
    /// 当前选中的进程（用于下拉框绑定）
    /// </summary>
    [ObservableProperty]
    private RunningProcess? _selectedProcess;

    /// <summary>
    /// 手动输入的进程名
    /// </summary>
    [ObservableProperty]
    private string _newProcessName = string.Empty;

    /// <summary>
    /// Popup 关闭请求事件（用于 View 关闭 Popup）
    /// </summary>
    public event EventHandler? ClosePopupRequested;

#endregion

    public WindowSettingsPageViewModel()
    {
        // 默认值，稍后通过 LoadSettings 从 Config 加载
    }

    /// <summary>
    /// 选中进程变化时自动添加到白名单
    /// </summary>
    partial void OnSelectedProcessChanged(RunningProcess? value)
    {
        if (value == null)
            return;

        // 添加到白名单（如果不存在）
        if (!ProcessWhitelist.Any(p => p.Equals(value.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            ProcessWhitelist.Add(value.ProcessName);
        }

        // 清空选择，允许再次选择同一项
        _selectedProcess = null;
        OnPropertyChanged(nameof(SelectedProcess));
    }

    /// <summary>
    /// 从配置对象加载设置
    /// </summary>
    public void LoadSettings(AppConfig config)
    {
        EnableEdgeSnap = config.EnableEdgeSnap;
        SnapThreshold = config.SnapThreshold;
        PromptRecordOnExit = config.PromptRecordOnExit;

        // 加载全局鼠标检测配置
        var cursorConfig = config.CursorDetection;

        System.Diagnostics.Debug.WriteLine(
            $"[WindowSettingsPage] LoadSettings: cursorConfig is null = {cursorConfig == null}");

        if (cursorConfig != null)
        {
            // 显式设置属性并触发通知
            CursorDetectionEnabled = cursorConfig.Enabled;
            CursorDetectionMinOpacity = cursorConfig.MinOpacity;
            CursorDetectionCheckIntervalMs = cursorConfig.CheckIntervalMs;
            CursorDetectionEnableDebugLog = cursorConfig.EnableDebugLog;

            // 显式触发属性变更通知
            OnPropertyChanged(nameof(CursorDetectionEnabled));
            OnPropertyChanged(nameof(CursorDetectionMinOpacity));
            OnPropertyChanged(nameof(CursorDetectionCheckIntervalMs));
            OnPropertyChanged(nameof(CursorDetectionEnableDebugLog));

            ProcessWhitelist.Clear();
            if (cursorConfig.ProcessWhitelist != null)
            {
                foreach (var process in cursorConfig.ProcessWhitelist)
                {
                    ProcessWhitelist.Add(process);
                }
            }

            // 通知 ProcessWhitelist 属性已更新（确保 Count 绑定刷新）
            OnPropertyChanged(nameof(ProcessWhitelist));

            System.Diagnostics.Debug.WriteLine(
                $"[WindowSettingsPage] LoadSettings: Enabled={cursorConfig.Enabled}, Whitelist.Count={cursorConfig.ProcessWhitelist?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine(
                $"[WindowSettingsPage] LoadSettings: After assignment, CursorDetectionEnabled={CursorDetectionEnabled}");
        }
        else
        {
            // 默认值
            CursorDetectionEnabled = false;
            CursorDetectionMinOpacity = 0.3;
            CursorDetectionCheckIntervalMs = 200;
            CursorDetectionEnableDebugLog = false;
            ProcessWhitelist.Clear();

            // 通知 ProcessWhitelist 属性已更新
            OnPropertyChanged(nameof(ProcessWhitelist));

            System.Diagnostics.Debug.WriteLine(
                $"[WindowSettingsPage] LoadSettings: Using defaults (cursorConfig is null)");
        }
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.EnableEdgeSnap = EnableEdgeSnap;
        config.SnapThreshold = SnapThreshold;
        config.PromptRecordOnExit = PromptRecordOnExit;

        // 保存全局鼠标检测配置
        var whitelist = ProcessWhitelist.ToList();
        config.CursorDetection =
            new GlobalCursorDetectionConfig { Enabled = CursorDetectionEnabled, MinOpacity = CursorDetectionMinOpacity,
                                              CheckIntervalMs = CursorDetectionCheckIntervalMs,
                                              EnableDebugLog = CursorDetectionEnableDebugLog,
                                              ProcessWhitelist = whitelist };

        // 调试日志
        System.Diagnostics.Debug.WriteLine(
            $"[WindowSettingsPage] SaveSettings: Enabled={CursorDetectionEnabled}, Whitelist.Count={whitelist.Count}");
        foreach (var p in whitelist)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowSettingsPage]   Whitelist item: {p}");
        }
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        LoadSettings(config);
    }

#region 白名单操作命令

    /// <summary>
    /// 手动添加进程到白名单
    /// </summary>
    [RelayCommand]
    private void AddProcess()
    {
        var processName = NewProcessName?.Trim();
        if (string.IsNullOrEmpty(processName))
            return;

        // 检查是否已存在（不区分大小写）
        if (ProcessWhitelist.Any(p => p.Equals(processName, StringComparison.OrdinalIgnoreCase)))
            return;

        ProcessWhitelist.Add(processName);
        NewProcessName = string.Empty;
    }

    /// <summary>
    /// 从 Popup 选择进程添加到白名单
    /// </summary>
    [RelayCommand]
    private void SelectProcessFromPopup(RunningProcess? process)
    {
        if (process == null)
            return;

        // 添加到白名单（如果不存在）
        if (!ProcessWhitelist.Any(p => p.Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            ProcessWhitelist.Add(process.ProcessName);
        }

        // 关闭 Popup
        ClosePopupRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 刷新运行中的窗口进程列表
    /// </summary>
    [RelayCommand]
    private void RefreshProcessList()
    {
        RunningProcesses.Clear();
        var processes = new System.Collections.Generic.List<RunningProcess>();
        var processNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        EnumWindows(
            (hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                // 获取窗口标题
                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                var sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                var windowTitle = sb.ToString();

                // 获取进程信息
                GetWindowThreadProcessId(hWnd, out uint processId);
                try
                {
                    var process = Process.GetProcessById((int)processId);
                    var processName = process.ProcessName;

                    // 跳过系统进程和自身
                    if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                        processName.Equals("AkashaNavigator", StringComparison.OrdinalIgnoreCase) ||
                        processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                        processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                        processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // 去重（同一进程可能有多个窗口）
                    if (!processNames.Contains(processName))
                    {
                        processNames.Add(processName);
                        processes.Add(new RunningProcess { ProcessName = processName,
                                                           WindowTitle = windowTitle.Length > 50
                                                                             ? windowTitle[..47] + "..."
                                                                             : windowTitle });
                    }
                }
                catch
                {
                    // 忽略无法访问的进程
                }

                return true;
            },
            IntPtr.Zero);

        // 按进程名排序
        foreach (var p in processes.OrderBy(p => p.ProcessName))
        {
            RunningProcesses.Add(p);
        }
    }

    /// <summary>
    /// 从白名单移除进程
    /// </summary>
    [RelayCommand]
    private void RemoveProcess(string processName)
    {
        ProcessWhitelist.Remove(processName);
    }

#endregion
}
