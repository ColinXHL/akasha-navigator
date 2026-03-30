using System;
using System.Collections.Generic;
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
    private readonly object _pressedLock = new();
    private readonly HashSet<uint> _pressedKeyboardKeys = new();
    private readonly HashSet<uint> _pressedMouseButtons = new();
    private readonly Dictionary<uint, long> _nextKeyboardRepeatAllowed = new();
    private readonly Dictionary<uint, long> _nextMouseRepeatAllowed = new();
    private const int DefaultRepeatDispatchIntervalMs = 90;
    private const int SeekRepeatDispatchIntervalMs = 25;
    private const int OpacityRepeatDispatchIntervalMs = 60;
    private const int PlaybackRateRepeatDispatchIntervalMs = 80;

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

    /// <summary>重置透明度事件</summary>
    public event EventHandler? ResetOpacity
    {
        add => _dispatcher.ResetOpacity += value;
        remove => _dispatcher.ResetOpacity -= value;
    }

    /// <summary>增加播放速率事件</summary>
    public event EventHandler? IncreasePlaybackRate
    {
        add => _dispatcher.IncreasePlaybackRate += value;
        remove => _dispatcher.IncreasePlaybackRate -= value;
    }

    /// <summary>减少播放速率事件</summary>
    public event EventHandler? DecreasePlaybackRate
    {
        add => _dispatcher.DecreasePlaybackRate += value;
        remove => _dispatcher.DecreasePlaybackRate -= value;
    }

    /// <summary>重置播放速率事件</summary>
    public event EventHandler? ResetPlaybackRate
    {
        add => _dispatcher.ResetPlaybackRate += value;
        remove => _dispatcher.ResetPlaybackRate -= value;
    }

    /// <summary>切换窗口可见性事件</summary>
    public event EventHandler? ToggleWindowVisibility
    {
        add => _dispatcher.ToggleWindowVisibility += value;
        remove => _dispatcher.ToggleWindowVisibility -= value;
    }

    /// <summary>暂停/恢复热键事件</summary>
    public event EventHandler? SuspendHotkeys
    {
        add => _dispatcher.SuspendHotkeys += value;
        remove => _dispatcher.SuspendHotkeys -= value;
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

        lock (_pressedLock)
        {
            _pressedKeyboardKeys.Clear();
            _pressedMouseButtons.Clear();
            _nextKeyboardRepeatAllowed.Clear();
            _nextMouseRepeatAllowed.Clear();
        }
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
    /// 注册插件快捷键（添加到当前激活的 Profile）
    /// </summary>
    /// <param name="vkCode">虚拟键码</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="actionName">动作名称</param>
    public void RegisterPluginHotkey(uint vkCode, Models.Config.ModifierKeys modifiers, string actionName)
    {
        var activeProfile = _config.GetActiveProfile();
        if (activeProfile == null)
        {
            Serilog.Log.Warning("RegisterPluginHotkey: No active profile found");
            return;
        }

        // 检查是否已存在相同的绑定
        var existingBinding = activeProfile.FindMatchingBinding(vkCode, modifiers, null);
        if (existingBinding != null)
        {
            // 已存在，更新动作名称
            existingBinding.Action = actionName;
            Serilog.Log.Debug("RegisterPluginHotkey: Updated existing binding for VK={VkCode}, Modifiers={Modifiers}, Action={Action}", vkCode, modifiers, actionName);
            return;
        }

        // 添加新绑定
        var binding = new HotkeyBinding
        {
            Key = vkCode,
            Modifiers = modifiers,
            Action = actionName,
            IsEnabled = true
        };
        activeProfile.Bindings.Add(binding);
        Serilog.Log.Information("RegisterPluginHotkey: Added new binding - VK={VkCode}, Modifiers={Modifiers}, Action={Action}, Total bindings={Count}", 
            vkCode, modifiers, actionName, activeProfile.Bindings.Count);
    }

    /// <summary>
    /// 注销插件快捷键
    /// </summary>
    /// <param name="actionName">动作名称</param>
    public void UnregisterPluginHotkey(string actionName)
    {
        var activeProfile = _config.GetActiveProfile();
        if (activeProfile == null)
            return;

        // 移除匹配的绑定
        activeProfile.Bindings.RemoveAll(b => b.Action == actionName);
    }

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

        Serilog.Log.Debug("OnKeyDown: VK={VkCode}, Modifiers={Modifiers}, Process={Process}, Profile={Profile}, Binding={Binding}", 
            vkCode, modifiers, processName, profile?.Name, binding?.Action);

        if (binding != null)
        {
            // 检查是否暂停（SuspendHotkeys 动作始终可用）
            if (_isSuspended &&
                !string.Equals(binding.Action, ActionSuspendHotkeys, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!ShouldDispatchKeyboard(vkCode, binding.Action))
            {
                return;
            }

            // 在 UI 线程上执行动作
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                                                                       {
                                                                           // 输入模式检测：焦点在输入控件时不触发快捷键
                                                                           if (IsInputMode())
                                                                           {
                                                                               Serilog.Log.Debug("OnKeyDown: Skipped due to input mode");
                                                                               return;
                                                                           }

                                                                           Serilog.Log.Information("OnKeyDown: Dispatching action {Action}", binding.Action);
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
        var vkCode = (uint)e.KeyCode;
        lock (_pressedLock)
        {
            _pressedKeyboardKeys.Remove(vkCode);
            _nextKeyboardRepeatAllowed.Remove(vkCode);
        }
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

            if (!ShouldDispatchMouse(mouseButton, binding.Action))
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
        if (e.Button == System.Windows.Forms.MouseButtons.XButton1)
        {
            lock (_pressedLock)
            {
                _pressedMouseButtons.Remove(MouseButtonCodes.XButton1);
                _nextMouseRepeatAllowed.Remove(MouseButtonCodes.XButton1);
            }
        }
        else if (e.Button == System.Windows.Forms.MouseButtons.XButton2)
        {
            lock (_pressedLock)
            {
                _pressedMouseButtons.Remove(MouseButtonCodes.XButton2);
                _nextMouseRepeatAllowed.Remove(MouseButtonCodes.XButton2);
            }
        }
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

    private bool ShouldDispatchKeyboard(uint vkCode, string actionName)
    {
        lock (_pressedLock)
        {
            return ShouldDispatchCore(vkCode, actionName, _pressedKeyboardKeys, _nextKeyboardRepeatAllowed);
        }
    }

    private bool ShouldDispatchMouse(uint mouseButton, string actionName)
    {
        lock (_pressedLock)
        {
            return ShouldDispatchCore(mouseButton, actionName, _pressedMouseButtons, _nextMouseRepeatAllowed);
        }
    }

    private static bool ShouldDispatchCore(uint key, string actionName, HashSet<uint> pressedKeys,
                                           Dictionary<uint, long> nextRepeatAllowed)
    {
        var nowMs = Environment.TickCount64;

        if (!pressedKeys.Contains(key))
        {
            pressedKeys.Add(key);
            nextRepeatAllowed[key] = nowMs + GetRepeatIntervalMs(actionName);
            return true;
        }

        if (!IsRepeatableAction(actionName))
        {
            return false;
        }

        if (!nextRepeatAllowed.TryGetValue(key, out var nextAllowedMs) || nowMs >= nextAllowedMs)
        {
            nextRepeatAllowed[key] = nowMs + GetRepeatIntervalMs(actionName);
            return true;
        }

        return false;
    }

    private static bool IsRepeatableAction(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return false;

        if (actionName.StartsWith("Plugin:", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.Equals(actionName, ActionDispatcher.ActionTogglePlay, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionToggleClickThrough,
                              StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionToggleMaximize, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionResetOpacity, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionResetPlaybackRate,
                              StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionToggleWindowVisibility,
                              StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(actionName, ActionDispatcher.ActionSuspendHotkeys, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetRepeatIntervalMs(string actionName)
    {
        if (string.Equals(actionName, ActionDispatcher.ActionSeekForward, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionName, ActionDispatcher.ActionSeekBackward, StringComparison.OrdinalIgnoreCase))
        {
            return SeekRepeatDispatchIntervalMs;
        }

        if (string.Equals(actionName, ActionDispatcher.ActionDecreaseOpacity, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionName, ActionDispatcher.ActionIncreaseOpacity, StringComparison.OrdinalIgnoreCase))
        {
            return OpacityRepeatDispatchIntervalMs;
        }

        if (string.Equals(actionName, ActionDispatcher.ActionIncreasePlaybackRate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionName, ActionDispatcher.ActionDecreasePlaybackRate, StringComparison.OrdinalIgnoreCase))
        {
            return PlaybackRateRepeatDispatchIntervalMs;
        }

        return DefaultRepeatDispatchIntervalMs;
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
