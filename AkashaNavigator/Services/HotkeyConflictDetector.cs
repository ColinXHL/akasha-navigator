using System;
using System.Collections.Generic;
using System.Linq;
using AkashaNavigator.Models.Config;

namespace AkashaNavigator.Services
{
/// <summary>
/// 快捷键冲突检测服务
/// </summary>
public class HotkeyConflictDetector
{
    /// <summary>
    /// 检测所有快捷键绑定中的冲突
    /// </summary>
    /// <param name="bindings">快捷键绑定列表</param>
    /// <returns>冲突组字典，键为签名，值为冲突的绑定列表</returns>
    public Dictionary<string, List<HotkeyBinding>> DetectConflicts(IEnumerable<HotkeyBinding> bindings)
    {
        var conflicts = new Dictionary<string, List<HotkeyBinding>>();

        var groups = bindings
                         .Where(b => b.Key != 0) // 忽略空绑定
                         .GroupBy(b => GetKeySignature(b))
                         .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            conflicts[group.Key] = group.ToList();
        }

        return conflicts;
    }

    /// <summary>
    /// 检查单个绑定是否与其他绑定冲突
    /// </summary>
    /// <param name="binding">要检查的绑定</param>
    /// <param name="allBindings">所有绑定列表</param>
    /// <returns>是否存在冲突</returns>
    public bool HasConflict(HotkeyBinding binding, IEnumerable<HotkeyBinding> allBindings)
    {
        // 空绑定不参与冲突检测
        if (binding.Key == 0)
            return false;

        var signature = GetKeySignature(binding);
        return allBindings.Where(b => !ReferenceEquals(b, binding) && b.Key != 0)
            .Any(b => GetKeySignature(b) == signature);
    }

    /// <summary>
    /// 获取与指定绑定冲突的其他绑定
    /// </summary>
    /// <param name="binding">要检查的绑定</param>
    /// <param name="allBindings">所有绑定列表</param>
    /// <returns>冲突的绑定列表</returns>
    public List<HotkeyBinding> GetConflictingBindings(HotkeyBinding binding, IEnumerable<HotkeyBinding> allBindings)
    {
        // 空绑定不参与冲突检测
        if (binding.Key == 0)
            return new List<HotkeyBinding>();

        var signature = GetKeySignature(binding);
        return allBindings.Where(b => !ReferenceEquals(b, binding) && b.Key != 0 && GetKeySignature(b) == signature)
            .ToList();
    }

    /// <summary>
    /// 获取绑定的唯一签名（用于冲突检测）
    /// </summary>
    /// <param name="binding">快捷键绑定</param>
    /// <returns>签名字符串</returns>
    public string GetKeySignature(HotkeyBinding binding)
    {
        return $"{(int)binding.InputType}:{binding.Key}:{(int)binding.Modifiers}";
    }
}
}
