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

    /// <summary>
    /// 是否处于老板键隐藏模式
    /// 隐藏模式下仅允许 ToggleWindowVisibility 动作通过，其他所有快捷键被拦截
    /// </summary>
    public bool IsBossKeyHidden { get; set; }

    /// <summary>
    /// 窗口隐藏时是否仍然触发快捷键（默认 false）
    /// 仅 ToggleWindowVisibility 和 SuspendHotkeys 始终可用
    /// </summary>
    public bool EnableHotkeysWhenHidden { get; set; }

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

    /// <summary>窥视按下事件（HotkeyService 原生事件，非代理）</summary>
    public event EventHandler? PeekPressed;

    /// <summary>窥视释放事件（HotkeyService 原生事件，非代理）</summary>
    public event EventHandler? PeekReleased;

    /// <summary>窥视按键配置</summary>
    private uint _peekKey;
    private Models.Config.ModifierKeys _peekModifiers;
    private bool _isPeekEnabled;
    private bool _isPeekHeld;

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
    /// 更新内置快捷键配置，同时保留运行中插件注册的快捷键。
    /// </summary>
    /// <remarks>
    /// 插件绑定是运行时注册的，未存储在 <see cref="AppConfig"/> 转换出的配置中。
    /// 因此，不能在更新内置配置时直接丢弃 <c>Plugin:</c> 动作，否则保存应用设置会使
    /// 已加载插件的快捷键失效。
    /// </remarks>
    /// <param name="config">新的内置快捷键配置</param>
    public void UpdateConfig(HotkeyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        PreservePluginBindings(_config, config);
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
    /// 配置窥视按键
    /// </summary>
    /// <param name="key">虚拟键码</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="enabled">是否启用窥视</param>
    public void SetPeekConfig(uint key, Models.Config.ModifierKeys modifiers, bool enabled)
    {
        _peekKey = key;
        _peekModifiers = modifiers;
        _isPeekEnabled = enabled;

        // 配置变化时清理 held 状态
        if (_isPeekHeld)
        {
            _isPeekHeld = false;
            PeekReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 注册插件快捷键（添加到当前激活的 Profile）。
    /// </summary>
    /// <param name="vkCode">虚拟键码</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="actionName">动作名称</param>
    /// <returns>成功注册或已存在相同动作时为 <see langword="true"/>；发生冲突时为 <see langword="false"/>。</returns>
    public bool RegisterPluginHotkey(uint vkCode, Models.Config.ModifierKeys modifiers, string actionName)
    {
        var activeProfile = _config.GetActiveProfile();
        if (activeProfile == null)
        {
            Serilog.Log.Warning("RegisterPluginHotkey: No active profile found");
            return false;
        }

        var inputType = HotkeyBinding.InferInputType(vkCode);
        var existingBinding = activeProfile.Bindings.FirstOrDefault(binding =>
            binding.IsEnabled &&
            binding.InputType == inputType &&
            binding.Key == vkCode &&
            binding.Modifiers == modifiers);
        if (existingBinding != null)
        {
            if (string.Equals(existingBinding.Action, actionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Serilog.Log.Warning(
                "RegisterPluginHotkey: Rejected conflicting binding for VK={VkCode}, Modifiers={Modifiers}. Existing action={ExistingAction}, requested action={RequestedAction}",
                vkCode, modifiers, existingBinding.Action, actionName);
            return false;
        }

        // 添加新绑定
        var binding = new HotkeyBinding
        {
            InputType = inputType,
            Key = vkCode,
            Modifiers = modifiers,
            Action = actionName,
            IsEnabled = true
        };
        activeProfile.Bindings.Add(binding);
        Serilog.Log.Information("RegisterPluginHotkey: Added new binding - VK={VkCode}, Modifiers={Modifiers}, Action={Action}, Total bindings={Count}", 
            vkCode, modifiers, actionName, activeProfile.Bindings.Count);
        return true;
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

    private static void PreservePluginBindings(HotkeyConfig currentConfig, HotkeyConfig updatedConfig)
    {
        foreach (var currentProfile in currentConfig.Profiles)
        {
            var pluginBindings = currentProfile.Bindings
                                               .Where(binding => IsPluginAction(binding.Action))
                                               .Select(CloneBinding)
                                               .ToList();
            if (pluginBindings.Count == 0)
            {
                continue;
            }

            var updatedProfile = updatedConfig.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, currentProfile.Name, StringComparison.OrdinalIgnoreCase));
            if (updatedProfile == null)
            {
                updatedProfile = new HotkeyProfile
                {
                    Name = currentProfile.Name,
                    ActivationProcesses = currentProfile.ActivationProcesses?.ToList()
                };
                updatedConfig.Profiles.Add(updatedProfile);
            }

            foreach (var pluginBinding in pluginBindings)
            {
                var alreadyPresent = updatedProfile.Bindings.Any(binding =>
                    string.Equals(binding.Action, pluginBinding.Action, StringComparison.OrdinalIgnoreCase));
                if (!alreadyPresent)
                {
                    updatedProfile.Bindings.Add(pluginBinding);
                }
            }
        }
    }

    private static bool IsPluginAction(string actionName)
    {
        return actionName.StartsWith("Plugin:", StringComparison.OrdinalIgnoreCase);
    }

    private static HotkeyBinding CloneBinding(HotkeyBinding binding)
    {
        return new HotkeyBinding
        {
            InputType = binding.InputType,
            Key = binding.Key,
            Modifiers = binding.Modifiers,
            Action = binding.Action,
            ProcessFilters = binding.ProcessFilters?.ToList(),
            IsEnabled = binding.IsEnabled
        };
    }

    /// <summary>
    /// 切换热键暂停状态
    /// </summary>
    public void ToggleSuspend()
    {
        _isSuspended = !_isSuspended;

        // 暂停时清理 peek held 状态
        if (_isSuspended && _isPeekHeld)
        {
            _isPeekHeld = false;
            PeekReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 清理窥视按键 held 状态（用于窗口隐藏、失去焦点等场景）
    /// </summary>
    public void ClearPeekHeld()
    {
        if (_isPeekHeld)
        {
            _isPeekHeld = false;
            PeekReleased?.Invoke(this, EventArgs.Empty);
        }
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

        // 窥视按键检测（在暂停检查前，但 boss key 模式下不处理）
        if (_isPeekEnabled && !IsBossKeyHidden &&
            vkCode == _peekKey && modifiers == _peekModifiers &&
            HotkeyBinding.InferInputType(vkCode) == Models.Config.InputType.Keyboard)
        {
            // 自动重复 KeyDown 不反复切换状态
            if (!_isPeekHeld)
            {
                _isPeekHeld = true;
                Serilog.Log.Debug("OnKeyDown: Peek key pressed (VK={VkCode})", vkCode);
                PeekPressed?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

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

            // 老板键隐藏模式：根据配置决定是否允许派发
            if (IsBossKeyHidden && !CanDispatchWhenHidden(binding.Action))
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

        // 窥视按键释放检测
        if (_isPeekEnabled && _isPeekHeld &&
            vkCode == _peekKey &&
            HotkeyBinding.InferInputType(vkCode) == Models.Config.InputType.Keyboard)
        {
            _isPeekHeld = false;
            Serilog.Log.Debug("OnKeyUp: Peek key released (VK={VkCode})", vkCode);
            PeekReleased?.Invoke(this, EventArgs.Empty);
        }

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

        // 窥视鼠标侧键检测
        if (_isPeekEnabled && !IsBossKeyHidden && mouseButton == _peekKey)
        {
            var peekMods = Win32Helper.GetCurrentModifiers();
            if (peekMods == _peekModifiers && !_isPeekHeld)
            {
                _isPeekHeld = true;
                Serilog.Log.Debug("OnMouseDown: Peek mouse button pressed (Button={Button})", mouseButton);
                PeekPressed?.Invoke(this, EventArgs.Empty);
                return;
            }
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

            // 老板键隐藏模式：根据配置决定是否允许派发
            if (IsBossKeyHidden && !CanDispatchWhenHidden(binding.Action))
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

        // 窥视鼠标侧键释放检测
        if (_isPeekEnabled && _isPeekHeld)
        {
            uint mouseButton = 0;
            if (e.Button == System.Windows.Forms.MouseButtons.XButton1)
                mouseButton = MouseButtonCodes.XButton1;
            else if (e.Button == System.Windows.Forms.MouseButtons.XButton2)
                mouseButton = MouseButtonCodes.XButton2;

            if (mouseButton == _peekKey)
            {
                _isPeekHeld = false;
                Serilog.Log.Debug("OnMouseUp: Peek mouse button released (Button={Button})", mouseButton);
                PeekReleased?.Invoke(this, EventArgs.Empty);
            }
        }

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
    /// 判断在老板键隐藏模式下是否允许派发指定动作
    /// ToggleWindowVisibility 始终可用，SuspendHotkeys 始终可用
    /// 其他动作是否允许取决于 EnableHotkeysWhenHidden 配置
    /// </summary>
    private bool CanDispatchWhenHidden(string actionName)
    {
        if (string.Equals(actionName, ActionDispatcher.ActionToggleWindowVisibility, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(actionName, ActionSuspendHotkeys, StringComparison.OrdinalIgnoreCase))
            return true;

        return EnableHotkeysWhenHidden;
    }

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
