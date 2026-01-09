using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using Gma.System.MouseKeyHook;

namespace AkashaNavigator.Services
{
/// <summary>
/// 全局快捷键服务，使用 MouseKeyHook 库实现
/// 支持配置驱动的快捷键绑定、组合键检测、进程过滤
/// 支持键盘和鼠标侧键作为快捷键触发器
/// 按键不会被拦截，既能触发快捷键功能，又能正常输入
/// 当焦点在输入控件时不触发快捷键
/// </summary>
public class HotkeyService : IDisposable
{
#region Fields

    private IKeyboardMouseEvents? _globalHook;
    private bool _isStarted;
    private bool _disposed;
    private bool _isSuspended;

    private HotkeyConfig _config;
    private readonly ActionDispatcher _dispatcher;

    /// <summary>暂停热键的动作名称</summary>
    public const string ActionSuspendHotkeys = "SuspendHotkeys";

#endregion

#region Properties

    /// <summary>
    /// 热键是否处于暂停状态
    /// </summary>
    public bool IsSuspended => _isSuspended;

#endregion

#region Events(兼容旧 API，代理到 ActionDispatcher)

    /// <summary>视频倒退事件</summary>
    public event EventHandler? SeekBackward
    {
        add => _dispatcher.SeekBackward += value;
        remove => _dispatcher.SeekBackward -= value;
    }

    /// <summary>视频前进事件</summary>
    public event EventHandler? SeekForward
    {
        add => _dispatcher.SeekForward += value;
        remove => _dispatcher.SeekForward -= value;
    }

    /// <summary>播放/暂停切换事件</summary>
    public event EventHandler? TogglePlay
    {
        add => _dispatcher.TogglePlay += value;
        remove => _dispatcher.TogglePlay -= value;
    }

    /// <summary>降低透明度事件</summary>
    public event EventHandler? DecreaseOpacity
    {
        add => _dispatcher.DecreaseOpacity += value;
        remove => _dispatcher.DecreaseOpacity -= value;
    }

    /// <summary>增加透明度事件</summary>
    public event EventHandler? IncreaseOpacity
    {
        add => _dispatcher.IncreaseOpacity += value;
        remove => _dispatcher.IncreaseOpacity -= value;
    }

    /// <summary>切换鼠标穿透模式事件</summary>
    public event EventHandler? ToggleClickThrough
    {
        add => _dispatcher.ToggleClickThrough += value;
        remove => _dispatcher.ToggleClickThrough -= value;
    }

    /// <summary>切换最大化事件</summary>
    public event EventHandler? ToggleMaximize
    {
        add => _dispatcher.ToggleMaximize += value;
        remove => _dispatcher.ToggleMaximize -= value;
    }

#endregion

#region Constructor

    /// <summary>
    /// 创建快捷键服务（使用默认配置）
    /// </summary>
    public HotkeyService() : this(HotkeyConfig.CreateDefault(), new ActionDispatcher())
    {
    }

    /// <summary>
    /// 创建快捷键服务
    /// </summary>
    /// <param name="config">快捷键配置</param>
    /// <param name="dispatcher">动作分发器</param>
    public HotkeyService(HotkeyConfig config, ActionDispatcher dispatcher)
    {
        _config = config;
        _dispatcher = dispatcher;
    }

#endregion

#region Public Methods

    /// <summary>
    /// 启动快捷键服务
    /// </summary>
    public void Start()
    {
        if (_isStarted)
            return;

        _globalHook = Hook.GlobalEvents();
        _globalHook.KeyDown += OnKeyDown;
        _globalHook.KeyUp += OnKeyUp;
        _globalHook.MouseDownExt += OnMouseDown;
        _globalHook.MouseUpExt += OnMouseUp;
        _isStarted = true;
    }

    /// <summary>
    /// 停止快捷键服务
    /// </summary>
    public void Stop()
    {
        if (!_isStarted)
            return;

        if (_globalHook != null)
        {
            _globalHook.KeyDown -= OnKeyDown;
            _globalHook.KeyUp -= OnKeyUp;
            _globalHook.MouseDownExt -= OnMouseDown;
            _globalHook.MouseUpExt -= OnMouseUp;
            _globalHook.Dispose();
            _globalHook = null;
        }

        _isStarted = false;
    }

    /// <summary>
    /// 更新快捷键配置
    /// </summary>
    /// <param name="config">新配置</param>
    public void UpdateConfig(HotkeyConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 获取当前配置
    /// </summary>
    /// <returns>当前快捷键配置</returns>
    public HotkeyConfig GetConfig() => _config;

    /// <summary>
    /// 获取动作分发器（用于注册自定义动作）
    /// </summary>
    /// <returns>动作分发器</returns>
    public ActionDispatcher GetDispatcher() => _dispatcher;

    /// <summary>
    /// 切换热键暂停状态
    /// </summary>
    public void ToggleSuspend()
    {
        _isSuspended = !_isSuspended;
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 键盘按下事件处理
    /// </summary>
    private void OnKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        // 不设置 Handled，让事件继续传递
        // e.Handled = false; // 默认就是 false

        var vkCode = (uint)e.KeyCode;

        // 获取当前修饰键状态
        var modifiers = Win32Helper.GetCurrentModifiers();

        // 获取前台进程名
        var processName = Win32Helper.GetForegroundWindowProcessName();

        // 查找匹配的绑定
        var profile = _config.FindProfileForProcess(processName);
        var binding = profile?.FindMatchingBinding(vkCode, modifiers, processName);

        if (binding != null)
        {
            // 检查是否暂停（SuspendHotkeys 动作始终可用）
            if (_isSuspended &&
                !string.Equals(binding.Action, ActionSuspendHotkeys, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 在 UI 线程上执行动作
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                                                                       {
                                                                           // 输入模式检测：焦点在输入控件时不触发快捷键
                                                                           if (IsInputMode())
                                                                               return;

                                                                           _dispatcher.Dispatch(binding.Action);
                                                                       });
        }
    }

    /// <summary>
    /// 键盘释放事件处理
    /// </summary>
    private void OnKeyUp(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        // 不设置 Handled，让事件继续传递
        // 目前不需要处理 KeyUp 事件
    }

    /// <summary>
    /// 鼠标按下事件处理（扩展版，支持侧键）
    /// </summary>
    private void OnMouseDown(object? sender, MouseEventExtArgs e)
    {
        // 不设置 Handled，让事件继续传递
        // e.Handled = false; // 默认就是 false

        // 仅处理鼠标侧键 (XButton1, XButton2)
        uint mouseButton = 0;
        if (e.Button == System.Windows.Forms.MouseButtons.XButton1)
        {
            mouseButton = MouseButtonCodes.XButton1;
        }
        else if (e.Button == System.Windows.Forms.MouseButtons.XButton2)
        {
            mouseButton = MouseButtonCodes.XButton2;
        }
        else
        {
            // 不处理其他鼠标按钮
            return;
        }

        // 获取当前修饰键状态
        var modifiers = Win32Helper.GetCurrentModifiers();

        // 获取前台进程名
        var processName = Win32Helper.GetForegroundWindowProcessName();

        // 查找匹配的鼠标绑定
        var profile = _config.FindProfileForProcess(processName);
        var binding = profile?.FindMatchingMouseBinding(mouseButton, modifiers, processName);

        if (binding != null)
        {
            // 检查是否暂停（SuspendHotkeys 动作始终可用）
            if (_isSuspended &&
                !string.Equals(binding.Action, ActionSuspendHotkeys, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 在 UI 线程上执行动作
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                                                                       {
                                                                           // 输入模式检测：焦点在输入控件时不触发快捷键
                                                                           if (IsInputMode())
                                                                               return;

                                                                           _dispatcher.Dispatch(binding.Action);
                                                                       });
        }
    }

    /// <summary>
    /// 鼠标释放事件处理
    /// </summary>
    private void OnMouseUp(object? sender, MouseEventExtArgs e)
    {
        // 不设置 Handled，让事件继续传递
        // 目前不需要处理 MouseUp 事件
    }

#endregion

#region Private Methods

    /// <summary>
    /// 检测当前是否处于输入模式（焦点在输入控件上）
    /// </summary>
    /// <returns>是否处于输入模式</returns>
    private static bool IsInputMode()
    {
        var focusedElement = Keyboard.FocusedElement;

        // 检查焦点元素是否为输入控件
        return focusedElement is TextBox || focusedElement is PasswordBox || focusedElement is RichTextBox ||
               focusedElement is TextBoxBase || focusedElement is ComboBox { IsEditable : true };
    }

#endregion

#region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Stop();
        }

        _disposed = true;
    }

    ~HotkeyService()
    {
        Dispose(false);
    }

#endregion
}
}
