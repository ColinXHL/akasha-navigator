using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Windows;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 快捷键设置页面 ViewModel
/// 包含：全局控制、视频控制、透明度、窗口行为、播放速率、窗口控制快捷键
/// </summary>
public partial class HotkeySettingsPageViewModel : ObservableObject
{
    private readonly HotkeyConflictDetector _conflictDetector;

    /// <summary>
    /// 快捷键绑定列表（MVVM 模式）
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> HotkeyBindings { get; } = new();

    /// <summary>
    /// 全局控制快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> GlobalHotkeys { get; } = new();

    /// <summary>
    /// 视频控制快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> VideoControlHotkeys { get; } = new();

    /// <summary>
    /// 透明度控制快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> OpacityControlHotkeys { get; } = new();

    /// <summary>
    /// 窗口行为快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> WindowBehaviorHotkeys { get; } = new();

    /// <summary>
    /// 播放速率控制快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> PlaybackRateHotkeys { get; } = new();

    /// <summary>
    /// 窗口控制快捷键
    /// </summary>
    public ObservableCollection<HotkeyBindingViewModel> WindowControlHotkeys { get; } = new();

    public HotkeySettingsPageViewModel()
    {
        _conflictDetector = new HotkeyConflictDetector();
    }

    /// <summary>
    /// 从配置对象加载快捷键
    /// </summary>
    public void LoadHotkeys(AppConfig config)
    {
        HotkeyBindings.Clear();
        GlobalHotkeys.Clear();
        VideoControlHotkeys.Clear();
        OpacityControlHotkeys.Clear();
        WindowBehaviorHotkeys.Clear();
        PlaybackRateHotkeys.Clear();
        WindowControlHotkeys.Clear();

        // 全局控制
        AddHotkeyBinding("SuspendHotkeys", "禁用/启用快捷键", config.HotkeySuspendHotkeys,
                         config.HotkeySuspendHotkeysMod, GlobalHotkeys);

        // 视频控制
        AddHotkeyBinding("SeekBackward", "视频倒退", config.HotkeySeekBackward, config.HotkeySeekBackwardMod,
                         VideoControlHotkeys);
        AddHotkeyBinding("SeekForward", "视频快进", config.HotkeySeekForward, config.HotkeySeekForwardMod,
                         VideoControlHotkeys);
        AddHotkeyBinding("TogglePlay", "播放/暂停", config.HotkeyTogglePlay, config.HotkeyTogglePlayMod,
                         VideoControlHotkeys);

        // 透明度控制
        AddHotkeyBinding("DecreaseOpacity", "降低透明度", config.HotkeyDecreaseOpacity,
                         config.HotkeyDecreaseOpacityMod, OpacityControlHotkeys);
        AddHotkeyBinding("IncreaseOpacity", "增加透明度", config.HotkeyIncreaseOpacity,
                         config.HotkeyIncreaseOpacityMod, OpacityControlHotkeys);
        AddHotkeyBinding("ResetOpacity", "重置透明度", config.HotkeyResetOpacity, config.HotkeyResetOpacityMod,
                         OpacityControlHotkeys);

        // 窗口行为
        AddHotkeyBinding("ToggleClickThrough", "鼠标穿透", config.HotkeyToggleClickThrough,
                         config.HotkeyToggleClickThroughMod, WindowBehaviorHotkeys);
        AddHotkeyBinding("ToggleMaximize", "切换最大化", config.HotkeyToggleMaximize, config.HotkeyToggleMaximizeMod,
                         WindowBehaviorHotkeys);

        // 播放速率控制
        AddHotkeyBinding("DecreasePlaybackRate", "减少播放速率", config.HotkeyDecreasePlaybackRate,
                         config.HotkeyDecreasePlaybackRateMod, PlaybackRateHotkeys);
        AddHotkeyBinding("IncreasePlaybackRate", "增加播放速率", config.HotkeyIncreasePlaybackRate,
                         config.HotkeyIncreasePlaybackRateMod, PlaybackRateHotkeys);
        AddHotkeyBinding("ResetPlaybackRate", "重置播放速率", config.HotkeyResetPlaybackRate,
                         config.HotkeyResetPlaybackRateMod, PlaybackRateHotkeys);

        // 窗口控制
        AddHotkeyBinding("ToggleWindowVisibility", "隐藏/显示窗口", config.HotkeyToggleWindowVisibility,
                         config.HotkeyToggleWindowVisibilityMod, WindowControlHotkeys);
    }

    /// <summary>
    /// 添加快捷键绑定
    /// </summary>
    private void AddHotkeyBinding(string action, string displayName, uint key, ModifierKeys modifiers,
                                  ObservableCollection<HotkeyBindingViewModel>? targetCollection = null)
    {
        var vm = new HotkeyBindingViewModel(action, displayName);
        vm.LoadFromConfig(key, modifiers);
        HotkeyBindings.Add(vm);
        targetCollection?.Add(vm);
    }

    /// <summary>
    /// 检测快捷键冲突并更新状态
    /// </summary>
    public void UpdateConflictStatus()
    {
        // 从 ViewModels 构建绑定列表
        var bindings = HotkeyBindings.Select(b => b.ToBinding()).ToList();

        // 检测冲突
        var conflicts = _conflictDetector.DetectConflicts(bindings);

        // 重置所有冲突状态
        foreach (var binding in HotkeyBindings)
        {
            binding.SetConflictStatus(false);
        }

        // 更新冲突状态
        foreach (var conflict in conflicts)
        {
            foreach (var binding in conflict.Value)
            {
                var vm = HotkeyBindings.FirstOrDefault(b => b.ActionName == binding.Action);
                if (vm != null)
                {
                    var otherActions = conflict.Value.Where(b => b.Action != binding.Action)
                                           .Select(b => GetActionDisplayName(b.Action));
                    vm.SetConflictStatus(true, $"与 {string.Join(", ", otherActions)} 冲突");
                }
            }
        }
    }

    /// <summary>
    /// 获取动作的显示名称
    /// </summary>
    private static string GetActionDisplayName(string actionName)
    {
        return actionName switch { "SeekBackward" => "视频倒退",
                                   "SeekForward" => "视频快进",
                                   "TogglePlay" => "播放/暂停",
                                   "DecreaseOpacity" => "降低透明度",
                                   "IncreaseOpacity" => "增加透明度",
                                   "ResetOpacity" => "重置透明度",
                                   "ToggleClickThrough" => "鼠标穿透",
                                   "DecreasePlaybackRate" => "减少播放速率",
                                   "IncreasePlaybackRate" => "增加播放速率",
                                   "ResetPlaybackRate" => "重置播放速率",
                                   "ToggleWindowVisibility" => "隐藏/显示窗口",
                                   "SuspendHotkeys" => "禁用/启用快捷键",
                                   "ToggleMaximize" => "切换最大化",
                                   _ => actionName };
    }

    /// <summary>
    /// 保存快捷键到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        // 保存快捷键（从 ViewModels 读取）
        foreach (var binding in HotkeyBindings)
        {
            switch (binding.ActionName)
            {
            case "SeekBackward":
                config.HotkeySeekBackward = binding.Key;
                config.HotkeySeekBackwardMod = binding.Modifiers;
                break;
            case "SeekForward":
                config.HotkeySeekForward = binding.Key;
                config.HotkeySeekForwardMod = binding.Modifiers;
                break;
            case "TogglePlay":
                config.HotkeyTogglePlay = binding.Key;
                config.HotkeyTogglePlayMod = binding.Modifiers;
                break;
            case "DecreaseOpacity":
                config.HotkeyDecreaseOpacity = binding.Key;
                config.HotkeyDecreaseOpacityMod = binding.Modifiers;
                break;
            case "IncreaseOpacity":
                config.HotkeyIncreaseOpacity = binding.Key;
                config.HotkeyIncreaseOpacityMod = binding.Modifiers;
                break;
            case "ResetOpacity":
                config.HotkeyResetOpacity = binding.Key;
                config.HotkeyResetOpacityMod = binding.Modifiers;
                break;
            case "ToggleClickThrough":
                config.HotkeyToggleClickThrough = binding.Key;
                config.HotkeyToggleClickThroughMod = binding.Modifiers;
                break;
            case "ToggleMaximize":
                config.HotkeyToggleMaximize = binding.Key;
                config.HotkeyToggleMaximizeMod = binding.Modifiers;
                break;
            case "DecreasePlaybackRate":
                config.HotkeyDecreasePlaybackRate = binding.Key;
                config.HotkeyDecreasePlaybackRateMod = binding.Modifiers;
                break;
            case "IncreasePlaybackRate":
                config.HotkeyIncreasePlaybackRate = binding.Key;
                config.HotkeyIncreasePlaybackRateMod = binding.Modifiers;
                break;
            case "ResetPlaybackRate":
                config.HotkeyResetPlaybackRate = binding.Key;
                config.HotkeyResetPlaybackRateMod = binding.Modifiers;
                break;
            case "ToggleWindowVisibility":
                config.HotkeyToggleWindowVisibility = binding.Key;
                config.HotkeyToggleWindowVisibilityMod = binding.Modifiers;
                break;
            case "SuspendHotkeys":
                config.HotkeySuspendHotkeys = binding.Key;
                config.HotkeySuspendHotkeysMod = binding.Modifiers;
                break;
            }
        }
    }

    /// <summary>
    /// 从配置对象重置快捷键
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        // 重置快捷键（更新现有 ViewModel 的值）
        foreach (var binding in HotkeyBindings)
        {
            switch (binding.ActionName)
            {
            case "SeekBackward":
                binding.LoadFromConfig(config.HotkeySeekBackward, config.HotkeySeekBackwardMod);
                break;
            case "SeekForward":
                binding.LoadFromConfig(config.HotkeySeekForward, config.HotkeySeekForwardMod);
                break;
            case "TogglePlay":
                binding.LoadFromConfig(config.HotkeyTogglePlay, config.HotkeyTogglePlayMod);
                break;
            case "DecreaseOpacity":
                binding.LoadFromConfig(config.HotkeyDecreaseOpacity, config.HotkeyDecreaseOpacityMod);
                break;
            case "IncreaseOpacity":
                binding.LoadFromConfig(config.HotkeyIncreaseOpacity, config.HotkeyIncreaseOpacityMod);
                break;
            case "ResetOpacity":
                binding.LoadFromConfig(config.HotkeyResetOpacity, config.HotkeyResetOpacityMod);
                break;
            case "ToggleClickThrough":
                binding.LoadFromConfig(config.HotkeyToggleClickThrough, config.HotkeyToggleClickThroughMod);
                break;
            case "ToggleMaximize":
                binding.LoadFromConfig(config.HotkeyToggleMaximize, config.HotkeyToggleMaximizeMod);
                break;
            case "DecreasePlaybackRate":
                binding.LoadFromConfig(config.HotkeyDecreasePlaybackRate, config.HotkeyDecreasePlaybackRateMod);
                break;
            case "IncreasePlaybackRate":
                binding.LoadFromConfig(config.HotkeyIncreasePlaybackRate, config.HotkeyIncreasePlaybackRateMod);
                break;
            case "ResetPlaybackRate":
                binding.LoadFromConfig(config.HotkeyResetPlaybackRate, config.HotkeyResetPlaybackRateMod);
                break;
            case "ToggleWindowVisibility":
                binding.LoadFromConfig(config.HotkeyToggleWindowVisibility, config.HotkeyToggleWindowVisibilityMod);
                break;
            case "SuspendHotkeys":
                binding.LoadFromConfig(config.HotkeySuspendHotkeys, config.HotkeySuspendHotkeysMod);
                break;
            }
        }
    }
}
