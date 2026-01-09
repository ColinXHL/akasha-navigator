using System;
using System.Collections.Generic;

namespace AkashaNavigator.Models.Config
{
/// <summary>
/// 修饰键标志位
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4
}

/// <summary>
/// 输入类型（键盘/鼠标）
/// </summary>
public enum InputType
{
    Keyboard = 0,
    Mouse = 1
}

/// <summary>
/// 鼠标按钮虚拟键码常量
/// </summary>
public static class MouseButtonCodes
{
    /// <summary>鼠标侧键1（后退按钮）</summary>
    public const uint XButton1 = 0x05;
    /// <summary>鼠标侧键2（前进按钮）</summary>
    public const uint XButton2 = 0x06;
}

/// <summary>
/// 快捷键绑定模型
/// </summary>
public class HotkeyBinding
{
    /// <summary>
    /// 输入类型（键盘/鼠标）
    /// </summary>
    public InputType InputType { get; set; } = InputType.Keyboard;

    /// <summary>
    /// 虚拟键码 (Win32 VK_xxx) 或鼠标按钮值
    /// </summary>
    public uint Key { get; set; }

    /// <summary>
    /// 修饰键组合 (Ctrl/Alt/Shift)
    /// </summary>
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

    /// <summary>
    /// 动作标识符 (如 "SeekBackward", "TogglePlay")
    /// 后续可扩展支持 "Script:xxx" 格式
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 进程过滤列表 (仅当前台进程在列表中时生效)
    /// null 或空列表表示全局生效
    /// </summary>
    public List<string>? ProcessFilters { get; set; }

    /// <summary>
    /// 是否启用此绑定
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 检查进程是否匹配过滤条件
    /// </summary>
    /// <param name="processName">当前前台进程名（不含路径）</param>
    /// <returns>是否匹配</returns>
    public bool MatchesProcess(string? processName)
    {
        // 无过滤条件 = 全局生效
        if (ProcessFilters == null || ProcessFilters.Count == 0)
            return true;

        if (string.IsNullOrEmpty(processName))
            return false;

        // 不区分大小写匹配
        foreach (var filter in ProcessFilters)
        {
            if (string.Equals(filter, processName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查按键和修饰键是否匹配（仅键盘输入）
    /// </summary>
    /// <param name="vkCode">按下的虚拟键码</param>
    /// <param name="currentModifiers">当前修饰键状态</param>
    /// <returns>是否匹配</returns>
    public bool MatchesKey(uint vkCode, ModifierKeys currentModifiers)
    {
        // 仅匹配键盘类型的绑定
        if (InputType != InputType.Keyboard)
            return false;

        return Key == vkCode && Modifiers == currentModifiers;
    }

    /// <summary>
    /// 检查鼠标按钮和修饰键是否匹配（仅鼠标输入）
    /// </summary>
    /// <param name="mouseButton">鼠标按钮值</param>
    /// <param name="currentModifiers">当前修饰键状态</param>
    /// <returns>是否匹配</returns>
    public bool MatchesMouseButton(uint mouseButton, ModifierKeys currentModifiers)
    {
        // 仅匹配鼠标类型的绑定
        if (InputType != InputType.Mouse)
            return false;

        return Key == mouseButton && Modifiers == currentModifiers;
    }
}
}
