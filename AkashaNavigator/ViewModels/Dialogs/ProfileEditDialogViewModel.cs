using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Dialogs
{
/// <summary>
/// Profile 编辑对话框 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class ProfileEditDialogViewModel : ObservableObject
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

    private readonly IProfileManager _profileManager;

    // 原始值跟踪字段（用于变更检测）
    private string _originalName = string.Empty;
    private string _originalIcon = string.Empty;
    private string _originalDefaultUrl = string.Empty;
    private double _originalDefaultOpacity = 1.0;
    private int _originalSeekSeconds = 5;
    private bool _originalCursorDetectionEnabled;
    private double _originalCursorDetectionMinOpacity = 0.3;
    private List<string> _originalProcessWhitelist = new();

    private string _profileId = string.Empty;
    private GameProfile? _profile;

    /// <summary>
    /// 可用图标列表
    /// </summary>
    public ObservableCollection<string> AvailableIcons { get; } = new();

#region 基本信息属性

    /// <summary>
    /// Profile 名称
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _profileName = string.Empty;

    /// <summary>
    /// 选中的图标
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _selectedIcon = "📦";

#endregion

#region 默认设置属性

    /// <summary>
    /// 默认 URL
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _defaultUrl = string.Empty;

    /// <summary>
    /// 默认透明度 (0.2 - 1.0)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private double _defaultOpacity = 1.0;

    /// <summary>
    /// 快进/倒退秒数 (1 - 60)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _seekSeconds = 5;

#endregion

#region 鼠标检测属性

    /// <summary>
    /// 鼠标检测是否启用
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _cursorDetectionEnabled;

    /// <summary>
    /// 鼠标检测最低透明度 (0.1 - 0.8)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private double _cursorDetectionMinOpacity = 0.3;

#endregion

#region 进程白名单属性

    /// <summary>
    /// 进程白名单
    /// </summary>
    public ObservableCollection<string> ProcessWhitelist { get; } = new();

    /// <summary>
    /// 新进程名称输入
    /// </summary>
    [ObservableProperty]
    private string _newProcessName = string.Empty;

    /// <summary>
    /// 运行中的进程列表
    /// </summary>
    public ObservableCollection<RunningProcess> RunningProcesses { get; } = new();

    /// <summary>
    /// 请求关闭 Popup 事件
    /// </summary>
    public event EventHandler? RequestClosePopup;

#endregion

#region 验证属性

    /// <summary>
    /// URL 验证错误消息
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _urlError;

    /// <summary>
    /// SeekSeconds 验证错误消息
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _seekSecondsError;

    /// <summary>
    /// 是否存在验证错误
    /// </summary>
    public bool HasValidationErrors => !string.IsNullOrEmpty(UrlError) || !string.IsNullOrEmpty(SeekSecondsError);

#endregion

    /// <summary>
    /// 错误消息
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 是否显示错误消息
    /// </summary>
    public bool ShowError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// 对话框结果
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// 请求关闭事件
    /// </summary>
    public event EventHandler<bool?>? RequestClose;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ProfileEditDialogViewModel(IProfileManager profileManager)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        LoadIcons();
    }

    /// <summary>
    /// 初始化方法
    /// </summary>
    public void Initialize(GameProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profileId = profile.Id;

        // 基本信息
        _originalName = profile.Name;
        _originalIcon = profile.Icon;
        ProfileName = profile.Name;
        SelectedIcon = profile.Icon;

        // 默认设置
        var defaults = profile.Defaults;
        _originalDefaultUrl = defaults?.Url ?? string.Empty;
        _originalDefaultOpacity = ClampOpacity(defaults?.Opacity ?? 1.0, 0.2, 1.0);
        _originalSeekSeconds = defaults?.SeekSeconds ?? 5;

        DefaultUrl = _originalDefaultUrl;
        DefaultOpacity = _originalDefaultOpacity;
        SeekSeconds = _originalSeekSeconds;

        // 鼠标检测配置
        var cursorDetection = profile.CursorDetection;
        _originalCursorDetectionEnabled = cursorDetection?.Enabled ?? false;
        _originalCursorDetectionMinOpacity = ClampOpacity(cursorDetection?.MinOpacity ?? 0.3, 0.1, 0.8);

        CursorDetectionEnabled = _originalCursorDetectionEnabled;
        CursorDetectionMinOpacity = _originalCursorDetectionMinOpacity;

        // 进程白名单
        _originalProcessWhitelist = cursorDetection?.ProcessWhitelist?.ToList() ?? new List<string>();
        ProcessWhitelist.Clear();
        foreach (var process in _originalProcessWhitelist)
        {
            ProcessWhitelist.Add(process);
        }

        ClearValidationErrors();
    }

    private void LoadIcons()
    {
        var icons = _profileManager.ProfileIcons;
        AvailableIcons.Clear();
        foreach (var icon in icons)
        {
            AvailableIcons.Add(icon);
        }
    }

#region 验证方法

    private void ValidateUrl()
    {
        var url = DefaultUrl?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            UrlError = null;
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            UrlError = "URL 格式无效";
        }
        else
        {
            UrlError = null;
        }
    }

    private void ValidateSeekSeconds()
    {
        SeekSecondsError = SeekSeconds < 1 || SeekSeconds > 60 ? "快进秒数必须在 1-60 之间" : null;
    }

    private void ClearValidationErrors()
    {
        UrlError = null;
        SeekSecondsError = null;
    }

#endregion

#region 属性变更处理

    partial void OnProfileNameChanged(string value) => ClearError();
    partial void OnDefaultUrlChanged(string value) => ValidateUrl();
    partial void OnSeekSecondsChanged(int value) => ValidateSeekSeconds();

    partial void OnDefaultOpacityChanged(double value)
    {
        if (value < 0.2)
            DefaultOpacity = 0.2;
        else if (value > 1.0)
            DefaultOpacity = 1.0;
    }

    partial void OnCursorDetectionMinOpacityChanged(double value)
    {
        if (value < 0.1)
            CursorDetectionMinOpacity = 0.1;
        else if (value > 0.8)
            CursorDetectionMinOpacity = 0.8;
    }

#endregion

#region 命令

    /// <summary>
    /// 保存 Profile
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!ValidateInput())
            return;

        var updateData = new ProfileUpdateData {
            Name = ProfileName.Trim(), Icon = SelectedIcon,
            Defaults = new ProfileDefaults { Url = string.IsNullOrWhiteSpace(DefaultUrl) ? null : DefaultUrl.Trim(),
                                             Opacity = DefaultOpacity, SeekSeconds = SeekSeconds }
        };

        // 鼠标检测配置 - 直接保存，有设置就覆盖全局
        var hasCursorDetectionSettings =
            CursorDetectionEnabled || Math.Abs(CursorDetectionMinOpacity - 0.3) > 0.001 || ProcessWhitelist.Count > 0;

        if (hasCursorDetectionSettings)
        {
            updateData.CursorDetection =
                new CursorDetectionConfig { Enabled = CursorDetectionEnabled, MinOpacity = CursorDetectionMinOpacity,
                                            ProcessWhitelist =
                                                ProcessWhitelist.Count > 0 ? ProcessWhitelist.ToList() : null };
            updateData.ClearCursorDetection = false;
        }
        else
        {
            updateData.CursorDetection = null;
            updateData.ClearCursorDetection = true;
        }

        var success = _profileManager.UpdateProfile(_profileId, updateData);

        if (success)
        {
            DialogResult = true;
            RequestClose?.Invoke(this, true);
        }
        else
        {
            SetError("保存失败");
        }
    }

    private bool CanSave()
    {
        if (HasValidationErrors)
            return false;
        if (string.IsNullOrWhiteSpace(ProfileName?.Trim()))
            return false;
        return HasChanges();
    }

    private bool HasChanges()
    {
        if (ProfileName?.Trim() != _originalName)
            return true;
        if (SelectedIcon != _originalIcon)
            return true;
        if ((DefaultUrl?.Trim() ?? string.Empty) != _originalDefaultUrl)
            return true;
        if (Math.Abs(DefaultOpacity - _originalDefaultOpacity) > 0.001)
            return true;
        if (SeekSeconds != _originalSeekSeconds)
            return true;
        if (CursorDetectionEnabled != _originalCursorDetectionEnabled)
            return true;
        if (Math.Abs(CursorDetectionMinOpacity - _originalCursorDetectionMinOpacity) > 0.001)
            return true;
        if (!ProcessWhitelist.SequenceEqual(_originalProcessWhitelist))
            return true;
        return false;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand]
    private void Close()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, null);
    }

    /// <summary>
    /// 添加进程到白名单
    /// </summary>
    [RelayCommand]
    private void AddProcess()
    {
        var processName = NewProcessName?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(processName))
            return;

        if (processName.EndsWith(".exe"))
            processName = processName[..^ 4];

        if (!ProcessWhitelist.Contains(processName))
        {
            ProcessWhitelist.Add(processName);
            SaveCommand.NotifyCanExecuteChanged();
        }

        NewProcessName = string.Empty;
    }

    /// <summary>
    /// 从白名单移除进程
    /// </summary>
    [RelayCommand]
    private void RemoveProcess(string processName)
    {
        if (ProcessWhitelist.Remove(processName))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 刷新运行中的进程列表
    /// </summary>
    [RelayCommand]
    private void RefreshProcessList()
    {
        RunningProcesses.Clear();
        var processes = new List<RunningProcess>();
        var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    var processName = process.ProcessName.ToLowerInvariant();

                    // 跳过系统进程和自身
                    if (processName == "explorer" || processName == "akashanavigator" ||
                        processName == "applicationframehost" || processName == "textinputhost" ||
                        processName == "shellexperiencehost")
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
    /// 从 Popup 选择进程
    /// </summary>
    [RelayCommand]
    private void SelectProcessFromPopup(RunningProcess process)
    {
        if (process == null)
            return;

        var processName = process.ProcessName;
        if (!ProcessWhitelist.Contains(processName))
        {
            ProcessWhitelist.Add(processName);
            SaveCommand.NotifyCanExecuteChanged();
        }

        RequestClosePopup?.Invoke(this, EventArgs.Empty);
    }

#endregion

#region 辅助方法

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(ProfileName?.Trim()))
        {
            SetError("Profile 名称不能为空");
            return false;
        }

        ValidateUrl();
        ValidateSeekSeconds();

        if (HasValidationErrors)
            return false;

        ClearError();
        return true;
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        OnPropertyChanged(nameof(ShowError));
    }

    private void ClearError()
    {
        ErrorMessage = null;
        OnPropertyChanged(nameof(ShowError));
    }

    private static double ClampOpacity(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

#endregion
}

/// <summary>
/// 运行中的进程信息
/// </summary>
public class RunningProcess
{
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
}
}
