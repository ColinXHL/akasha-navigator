using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using ConfigModifierKeys = AkashaNavigator.Models.Config.ModifierKeys;
using ConfigInputType = AkashaNavigator.Models.Config.InputType;

namespace AkashaNavigator.Controls
{
/// <summary>
/// 快捷键输入框自定义控件
/// 封装快捷键编辑逻辑，符合 MVVM 模式
/// </summary>
public class HotkeyTextBox : System.Windows.Controls.TextBox
{
    #region Dependency Properties

    /// <summary>
    /// 虚拟键码依赖属性
    /// </summary>
    public static readonly DependencyProperty HotkeyValueProperty =
        DependencyProperty.Register(
            nameof(HotkeyValue),
            typeof(uint),
            typeof(HotkeyTextBox),
            new PropertyMetadata(0u, OnHotkeyValuePropertyChanged));

    /// <summary>
    /// 修饰键依赖属性
    /// </summary>
    public static readonly DependencyProperty ModifiersProperty =
        DependencyProperty.Register(
            nameof(Modifiers),
            typeof(ConfigModifierKeys),
            typeof(HotkeyTextBox),
            new PropertyMetadata(ConfigModifierKeys.None, OnModifiersPropertyChanged));

    /// <summary>
    /// 输入类型依赖属性
    /// </summary>
    public static readonly DependencyProperty InputTypeProperty =
        DependencyProperty.Register(
            nameof(InputType),
            typeof(ConfigInputType),
            typeof(HotkeyTextBox),
            new PropertyMetadata(ConfigInputType.Keyboard, OnInputTypePropertyChanged));

    #endregion

    #region Properties

    /// <summary>
    /// 虚拟键码（双向绑定）
    /// </summary>
    public uint HotkeyValue
    {
        get => (uint)GetValue(HotkeyValueProperty);
        set => SetValue(HotkeyValueProperty, value);
    }

    /// <summary>
    /// 修饰键（双向绑定）
    /// </summary>
    public ConfigModifierKeys Modifiers
    {
        get => (ConfigModifierKeys)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    /// <summary>
    /// 输入类型（双向绑定）
    /// </summary>
    public ConfigInputType InputType
    {
        get => (ConfigInputType)GetValue(InputTypeProperty);
        set => SetValue(InputTypeProperty, value);
    }

    #endregion

    #region Fields

    private string _originalText = string.Empty;
    private ImeHelper.ImeState _savedImeState;
    private bool _isProcessingKey;
    private bool _recording;
    private System.Windows.Threading.DispatcherTimer? _lostFocusTimer;

    // 所有 HotkeyTextBox 共用一个低级键盘钩子，并把输入路由给当前获得焦点的控件。
    // 这样在两个输入框快速切换时，不会出现一个实例释放另一个实例仍在使用的 delegate/hook。
    private static IntPtr _keyboardHook;
    private static Win32Helper.LowLevelKeyboardProc? _keyboardHookProc;
    private static HotkeyTextBox? _activeRecorder;

    // 录键期间临时移除 WS_SYSMENU。记录原始位并精确恢复，避免给无边框窗口永久加上系统菜单。
    private static IntPtr _systemMenuHwnd;
    private static bool _systemMenuWasEnabled;

    // 系统保留/修饰 VK 码
    private const uint VK_TAB = 0x09;
    private const uint VK_SHIFT = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_MENU = 0x12;
    private const uint VK_ESCAPE = 0x1B;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint VK_F4 = 0x73;
    private const uint VK_F10 = 0x79;
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint LLKHF_ALTDOWN = 0x20;

    #endregion

    #region Constructor

    static HotkeyTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyTextBox),
            new FrameworkPropertyMetadata(typeof(HotkeyTextBox)));
    }

    public HotkeyTextBox()
    {
        TextAlignment = TextAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsReadOnly = false;  // 改为 false，通过 PreviewTextInput 阻止文本输入

        UpdateDisplayText();

        PreviewKeyDown += OnPreviewKeyDown;
        KeyDown += OnKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewTextInput += OnPreviewTextInput;  // 阻止文本输入
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 阻止文本输入，只允许快捷键编辑
    /// </summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;  // 阻止所有文本输入
    }

    #endregion

    #region Event Handlers

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _originalText = Text;
        Text = "按下新快捷键...";

        _recording = true;
        LostFocusTimer.Stop();

        // Alt 组合键会被 WPF 的 AccessKey/系统菜单吞掉，需在系统输入流源头拦截。
        // 钩子和系统菜单状态由所有输入框共享，快速切换输入框时只转移“当前录制者”。
        ActivateKeyboardRecording();

        // 切换到英文输入模式
        _savedImeState = ImeHelper.SwitchToEnglish(Window.GetWindow(this));
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        UpdateDisplayText();
        ImeHelper.RestoreImeState(_savedImeState);

        // 延迟 1s 结束录键：Alt 按下时系统可能临时转移焦点，给组合键留录入时间
        LostFocusTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _lostFocusTimer?.Stop();
        _recording = false;
        DeactivateKeyboardRecording();
    }

    /// <summary>
    /// 录键状态延迟清除计时器（单实例，Tick 只订阅一次）
    /// </summary>
    private System.Windows.Threading.DispatcherTimer LostFocusTimer
    {
        get
        {
            if (_lostFocusTimer == null)
            {
                _lostFocusTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _lostFocusTimer.Tick += (_, _) =>
                {
                    _lostFocusTimer.Stop();
                    if (ReferenceEquals(_activeRecorder, this))
                    {
                        _recording = false;
                        DeactivateKeyboardRecording();
                    }
                };
            }
            return _lostFocusTimer;
        }
    }

    /// <summary>
    /// 激活当前输入框的 Alt 组合键录制。
    /// 所有 HotkeyTextBox 共用一个 WH_KEYBOARD_LL，避免多个实例同时持有静态 delegate。
    /// </summary>
    private void ActivateKeyboardRecording()
    {
        if (_activeRecorder != null && !ReferenceEquals(_activeRecorder, this))
        {
            _activeRecorder._recording = false;
            _activeRecorder._lostFocusTimer?.Stop();
        }

        _activeRecorder = this;

        if (_keyboardHook == IntPtr.Zero)
        {
            _keyboardHookProc = KeyboardHookCallback;
            _keyboardHook = Win32Helper.SetKeyboardHook(_keyboardHookProc);
            if (_keyboardHook == IntPtr.Zero)
            {
                _keyboardHookProc = null;
            }
        }

        DisableSystemMenuForRecorder(this);
    }

    /// <summary>
    /// 仅当前录制者可以释放共享钩子，并恢复进入录键前的系统菜单位。
    /// </summary>
    private void DeactivateKeyboardRecording()
    {
        if (!ReferenceEquals(_activeRecorder, this))
            return;

        _activeRecorder = null;
        RestoreSystemMenu();

        if (_keyboardHook != IntPtr.Zero)
        {
            Win32Helper.RemoveKeyboardHook(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _keyboardHookProc = null;
    }

    /// <summary>
    /// 全局键盘钩子回调：只捕获当前录制者的 Alt 组合键并吞掉最终按键。
    /// </summary>
    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var recorder = _activeRecorder;
        if (nCode >= 0 && recorder?._recording == true)
        {
            int msg = wParam.ToInt32();
            if (msg == Win32Helper.WM_KEYDOWN || msg == Win32Helper.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<Win32Helper.KBDLLHOOKSTRUCT>(lParam);
                uint vk = data.vkCode;

                if ((data.flags & LLKHF_ALTDOWN) != 0 && IsRecordableCombo(vk))
                {
                    var modifiers = ConfigModifierKeys.Alt;
                    if (Win32Helper.IsKeyPressed(Win32Helper.VK_CONTROL))
                        modifiers |= ConfigModifierKeys.Ctrl;
                    if (Win32Helper.IsKeyPressed(Win32Helper.VK_SHIFT))
                        modifiers |= ConfigModifierKeys.Shift;

                    recorder._isProcessingKey = true;
                    try
                    {
                        recorder.SetHotkey(vk, modifiers, ConfigInputType.Keyboard);
                    }
                    finally
                    {
                        recorder._isProcessingKey = false;
                    }

                    return (IntPtr)1;
                }
            }
        }

        return Win32Helper.CallNextHook(_keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Alt 组合键中可录入的主键。
    /// 修饰键必须继续传递，等待真正的主键到来，才能正确录入 Alt+Ctrl/Shift+Key。
    /// </summary>
    internal static bool IsRecordableCombo(uint vk) =>
        vk != VK_SHIFT &&
        vk != VK_CONTROL &&
        vk != VK_MENU &&
        vk != VK_LSHIFT &&
        vk != VK_RSHIFT &&
        vk != VK_LCONTROL &&
        vk != VK_RCONTROL &&
        vk != VK_LMENU &&
        vk != VK_RMENU &&
        vk != VK_LWIN &&
        vk != VK_RWIN &&
        vk != VK_TAB &&
        vk != VK_ESCAPE &&
        vk != VK_F4 &&
        vk != VK_F10;

    private static void DisableSystemMenuForRecorder(HotkeyTextBox recorder)
    {
        var window = Window.GetWindow(recorder);
        if (window == null)
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        if (_systemMenuHwnd != IntPtr.Zero && _systemMenuHwnd != hwnd)
        {
            RestoreSystemMenu();
        }

        // 同一窗口内从一个 HotkeyTextBox 切换到另一个时保留最初记录的原始状态。
        if (_systemMenuHwnd == hwnd)
            return;

        var style = Win32Helper.GetWindowStyle(hwnd);
        _systemMenuHwnd = hwnd;
        _systemMenuWasEnabled = (style & Win32Helper.WS_SYSMENU) != 0;

        if (_systemMenuWasEnabled)
        {
            Win32Helper.SetWindowStyle(hwnd, style & ~Win32Helper.WS_SYSMENU);
        }
    }

    private static void RestoreSystemMenu()
    {
        if (_systemMenuHwnd == IntPtr.Zero)
            return;

        var style = Win32Helper.GetWindowStyle(_systemMenuHwnd);
        var restoredStyle = _systemMenuWasEnabled
            ? style | Win32Helper.WS_SYSMENU
            : style & ~Win32Helper.WS_SYSMENU;

        if (restoredStyle != style)
        {
            Win32Helper.SetWindowStyle(_systemMenuHwnd, restoredStyle);
        }

        _systemMenuHwnd = IntPtr.Zero;
        _systemMenuWasEnabled = false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // ESC 键备用处理
        if (e.Key == Key.Escape && !e.Handled)
        {
            e.Handled = true;
            ClearHotkey();
            MoveFocusToWindow();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isProcessingKey)
            return;

        _isProcessingKey = true;

        try
        {
            Key targetKey = e.Key;
            bool isSystemKey = e.Key == Key.System;

            if (isSystemKey)
            {
                // Alt 组合键可能以两种形态到达：
                // 1) 普通键（IsSystem=False，日志实证：真实场景组合键走这里）
                // 2) 系统键（IsSystem=True，SystemKey=实际键）
                // 两种都要处理，此处恢复 SystemKey 解析
                targetKey = e.SystemKey;

                // 排除系统级快捷键（Alt+Tab 等）
                if (targetKey == Key.Tab)
                {
                    return;  // 不处理，让系统处理
                }
            }

            // ESC 键：清空快捷键绑定
            if (targetKey == Key.Escape)
            {
                e.Handled = true;
                ClearHotkey();
                MoveFocusToWindow();
                return;
            }

            // 忽略修饰键本身
            if (IsModifierKey(targetKey))
            {
                e.Handled = true;
                return;
            }

            // 获取虚拟键码
            var vkCode = (uint)KeyInterop.VirtualKeyFromKey(targetKey);

            // 获取当前修饰键状态
            var modifiers = GetModifierKeys(isSystemKey);

            e.Handled = true;
            SetHotkey(vkCode, modifiers, ConfigInputType.Keyboard);
        }
        finally
        {
            _isProcessingKey = false;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isProcessingKey)
            return;

        uint mouseButton = e.ChangedButton switch
        {
            MouseButton.XButton1 => MouseButtonCodes.XButton1,
            MouseButton.XButton2 => MouseButtonCodes.XButton2,
            _ => 0
        };

        if (mouseButton == 0)
            return;

        _isProcessingKey = true;

        try
        {
            e.Handled = true;
            var modifiers = GetModifierKeys(isSystemKey: false);
            SetHotkey(mouseButton, modifiers, ConfigInputType.Mouse);
        }
        finally
        {
            _isProcessingKey = false;
        }
    }

    #endregion

    #region Private Methods

    private static void OnHotkeyValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            // 总是更新显示文本
            textBox.UpdateDisplayText();
        }
    }

    private static void OnModifiersPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            // 总是更新显示文本
            textBox.UpdateDisplayText();
        }
    }

    private static void OnInputTypePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            textBox.UpdateDisplayText();
        }
    }

    /// <summary>
    /// 判断是否为修饰键
    /// </summary>
    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin;
    }

    /// <summary>
    /// 获取当前修饰键状态
    /// （Alt 组合键由全局钩子处理，此处服务于 Ctrl/Shift 组合与鼠标按键）
    /// </summary>
    private static ConfigModifierKeys GetModifierKeys(bool isSystemKey)
    {
        var modifiers = ConfigModifierKeys.None;
        if (Win32Helper.IsKeyPressed(Win32Helper.VK_CONTROL))
            modifiers |= ConfigModifierKeys.Ctrl;
        if (isSystemKey || Win32Helper.IsKeyPressed(Win32Helper.VK_MENU))
            modifiers |= ConfigModifierKeys.Alt;
        if (Win32Helper.IsKeyPressed(Win32Helper.VK_SHIFT))
            modifiers |= ConfigModifierKeys.Shift;
        return modifiers;
    }

    /// <summary>
    /// 更新显示文本
    /// </summary>
    private void UpdateDisplayText()
    {
        Text = HotkeyValue == 0 ? string.Empty : Win32Helper.GetHotkeyDisplayName(HotkeyValue, Modifiers);
    }

    /// <summary>
    /// 清空快捷键
    /// </summary>
    private void ClearHotkey()
    {
        HotkeyValue = 0;
        Modifiers = ConfigModifierKeys.None;
        InputType = ConfigInputType.Keyboard;
    }

    private void SetHotkey(uint key, ConfigModifierKeys modifiers, ConfigInputType inputType)
    {
        HotkeyValue = key;
        Modifiers = modifiers;
        InputType = inputType;
        UpdateDisplayText();
        MoveFocusToWindow();
    }

    /// <summary>
    /// 将焦点移回窗口
    /// </summary>
    private void MoveFocusToWindow()
    {
        // 延迟执行，确保 LostFocus 事件先触发
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // 强制清除焦点
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
            Keyboard.ClearFocus();

            // 将焦点设置到窗口，而不是控件本身
            var window = Window.GetWindow(this);
            if (window != null)
            {
                // 设置焦点到窗口，但不让任何子元素获得焦点
                window.Focusable = true;
                window.Focus();
                Keyboard.Focus(null);
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    #endregion
}
}
