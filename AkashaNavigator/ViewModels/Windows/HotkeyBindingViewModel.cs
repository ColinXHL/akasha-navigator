using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Helpers;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 快捷键绑定视图模型，包装 HotkeyBinding 并添加冲突检测支持
/// </summary>
public partial class HotkeyBindingViewModel : ObservableObject
{
    private readonly string _actionName;
    private readonly string _displayName;

    /// <summary>
    /// 动作名称（如 "SeekBackward"）
    /// </summary>
    public string ActionName => _actionName;

    /// <summary>
    /// 显示名称（如 "视频倒退"）
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 虚拟键码
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyDisplayText))]
    private uint _key;

    /// <summary>
    /// 修饰键
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyDisplayText))]
    private ModifierKeys _modifiers = ModifierKeys.None;

    /// <summary>
    /// 输入类型（键盘/鼠标）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyDisplayText))]
    private InputType _inputType = InputType.Keyboard;

    /// <summary>
    /// 是否存在冲突
    /// </summary>
    [ObservableProperty]
    private bool _hasConflict;

    /// <summary>
    /// 冲突提示信息
    /// </summary>
    [ObservableProperty]
    private string _conflictTooltip = string.Empty;

    /// <summary>
    /// 快捷键显示文本
    /// </summary>
    public string HotkeyDisplayText
    {
        get {
            if (Key == 0)
                return string.Empty;
            return Win32Helper.GetHotkeyDisplayName(Key, Modifiers);
        }
    }

    /// <summary>
    /// 绑定变化事件
    /// </summary>
    public event EventHandler? BindingChanged;

    public HotkeyBindingViewModel(string actionName, string displayName)
    {
        _actionName = actionName ?? throw new ArgumentNullException(nameof(actionName));
        _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>
    /// 从配置值加载
    /// </summary>
    public void LoadFromConfig(uint key, ModifierKeys modifiers, InputType inputType = InputType.Keyboard)
    {
        Key = key;
        Modifiers = modifiers;
        InputType = inputType;
        OnPropertyChanged(nameof(HotkeyDisplayText));
        // 触发绑定变化事件，以便冲突检测等逻辑能够运行
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 设置快捷键
    /// </summary>
    public void SetHotkey(uint key, ModifierKeys modifiers, InputType inputType = InputType.Keyboard)
    {
        Key = key;
        Modifiers = modifiers;
        InputType = inputType;
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清空快捷键
    /// </summary>
    public void ClearHotkey()
    {
        Key = 0;
        Modifiers = ModifierKeys.None;
        BindingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 设置冲突状态
    /// </summary>
    public void SetConflictStatus(bool hasConflict, string tooltip = "")
    {
        HasConflict = hasConflict;
        ConflictTooltip =
            hasConflict ? (string.IsNullOrEmpty(tooltip) ? "此快捷键与其他动作冲突" : tooltip) : string.Empty;
    }

    /// <summary>
    /// 获取用于冲突检测的签名
    /// </summary>
    public string GetKeySignature()
    {
        return $"{(int)InputType}:{Key}:{(int)Modifiers}";
    }

    /// <summary>
    /// 转换为 HotkeyBinding 模型
    /// </summary>
    public HotkeyBinding ToBinding()
    {
        return new HotkeyBinding { InputType = InputType, Key = Key, Modifiers = Modifiers, Action = ActionName,
                                   IsEnabled = true };
    }
}
}
