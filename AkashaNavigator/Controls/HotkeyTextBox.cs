using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using ConfigModifierKeys = AkashaNavigator.Models.Config.ModifierKeys;

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

    #endregion

    #region Fields

    private string _originalText = string.Empty;
    private ImeHelper.ImeState _savedImeState;
    private bool _isProcessingKey;

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
        PreviewTextInput += OnPreviewTextInput;  // 阻止文本输入
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
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

        // 切换到英文输入模式
        _savedImeState = ImeHelper.SwitchToEnglish(Window.GetWindow(this));
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        UpdateDisplayText();

        // 恢复之前的输入法状态
        ImeHelper.RestoreImeState(_savedImeState);
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

            // 更新绑定属性（触发双向绑定）
            HotkeyValue = vkCode;
            Modifiers = modifiers;

            // 刷新显示
            UpdateDisplayText();

            // 将焦点返回给窗口
            MoveFocusToWindow();
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
    /// </summary>
    private static ConfigModifierKeys GetModifierKeys(bool isSystemKey)
    {
        var modifiers = ConfigModifierKeys.None;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            modifiers |= ConfigModifierKeys.Ctrl;
        if (isSystemKey || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= ConfigModifierKeys.Alt;
        if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
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
