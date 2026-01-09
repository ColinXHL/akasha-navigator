using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 设置窗口 ViewModel - MVVM 架构
/// 快捷键通过 HotkeyTextBox 自定义控件实现双向绑定
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
    private readonly IEventBus _eventBus;
    private readonly HotkeyConflictDetector _conflictDetector;
    private AppConfig _config;

    /// <summary>
    /// 可用 Profile 列表
    /// </summary>
    public ObservableCollection<GameProfile> Profiles { get; } = new();

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

    /// <summary>
    /// 快进秒数（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _seekSeconds;

    /// <summary>
    /// 透明度百分比（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _opacityPercent;

    /// <summary>
    /// 是否启用边缘吸附（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableEdgeSnap;

    /// <summary>
    /// 吸附阈值（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private int _snapThreshold;

    /// <summary>
    /// 是否在退出时提示记录笔记（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _promptRecordOnExit;

    /// <summary>
    /// 是否启用插件更新通知（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enablePluginUpdateNotification;

    /// <summary>
    /// 是否启用调试日志（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _enableDebugLog;

    /// <summary>
    /// 当前选中的 Profile（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnsubscribeProfileCommand))]
    private GameProfile? _selectedProfile;

    public SettingsViewModel(IConfigService configService, IProfileManager profileManager, IEventBus eventBus)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _conflictDetector = new HotkeyConflictDetector();

        _config = _configService.Config;
        LoadSettings();
        LoadHotkeys();

        // 订阅快捷键变化事件，自动检测冲突
        foreach (var binding in HotkeyBindings)
        {
            binding.BindingChanged += (s, e) => UpdateConflictStatus();
        }
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
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        SeekSeconds = _config.SeekSeconds;
        OpacityPercent = (int)(_config.DefaultOpacity * 100);
        EnableEdgeSnap = _config.EnableEdgeSnap;
        SnapThreshold = _config.SnapThreshold;
        PromptRecordOnExit = _config.PromptRecordOnExit;
        EnablePluginUpdateNotification = _config.EnablePluginUpdateNotification;
        EnableDebugLog = _config.EnableDebugLog;
    }

    /// <summary>
    /// 加载快捷键
    /// </summary>
    private void LoadHotkeys()
    {
        HotkeyBindings.Clear();
        GlobalHotkeys.Clear();
        VideoControlHotkeys.Clear();
        OpacityControlHotkeys.Clear();
        WindowBehaviorHotkeys.Clear();
        PlaybackRateHotkeys.Clear();
        WindowControlHotkeys.Clear();

        // 全局控制
        AddHotkeyBinding("SuspendHotkeys", "禁用/启用快捷键", _config.HotkeySuspendHotkeys,
                         _config.HotkeySuspendHotkeysMod, GlobalHotkeys);

        // 视频控制
        AddHotkeyBinding("SeekBackward", "视频倒退", _config.HotkeySeekBackward, _config.HotkeySeekBackwardMod,
                         VideoControlHotkeys);
        AddHotkeyBinding("SeekForward", "视频快进", _config.HotkeySeekForward, _config.HotkeySeekForwardMod,
                         VideoControlHotkeys);
        AddHotkeyBinding("TogglePlay", "播放/暂停", _config.HotkeyTogglePlay, _config.HotkeyTogglePlayMod,
                         VideoControlHotkeys);

        // 透明度控制
        AddHotkeyBinding("DecreaseOpacity", "降低透明度", _config.HotkeyDecreaseOpacity,
                         _config.HotkeyDecreaseOpacityMod, OpacityControlHotkeys);
        AddHotkeyBinding("IncreaseOpacity", "增加透明度", _config.HotkeyIncreaseOpacity,
                         _config.HotkeyIncreaseOpacityMod, OpacityControlHotkeys);
        AddHotkeyBinding("ResetOpacity", "重置透明度", _config.HotkeyResetOpacity, _config.HotkeyResetOpacityMod,
                         OpacityControlHotkeys);

        // 窗口行为
        AddHotkeyBinding("ToggleClickThrough", "鼠标穿透", _config.HotkeyToggleClickThrough,
                         _config.HotkeyToggleClickThroughMod, WindowBehaviorHotkeys);
        AddHotkeyBinding("ToggleMaximize", "切换最大化", _config.HotkeyToggleMaximize, _config.HotkeyToggleMaximizeMod,
                         WindowBehaviorHotkeys);

        // 播放速率控制
        AddHotkeyBinding("DecreasePlaybackRate", "减少播放速率", _config.HotkeyDecreasePlaybackRate,
                         _config.HotkeyDecreasePlaybackRateMod, PlaybackRateHotkeys);
        AddHotkeyBinding("IncreasePlaybackRate", "增加播放速率", _config.HotkeyIncreasePlaybackRate,
                         _config.HotkeyIncreasePlaybackRateMod, PlaybackRateHotkeys);
        AddHotkeyBinding("ResetPlaybackRate", "重置播放速率", _config.HotkeyResetPlaybackRate,
                         _config.HotkeyResetPlaybackRateMod, PlaybackRateHotkeys);

        // 窗口控制
        AddHotkeyBinding("ToggleWindowVisibility", "隐藏/显示窗口", _config.HotkeyToggleWindowVisibility,
                         _config.HotkeyToggleWindowVisibilityMod, WindowControlHotkeys);
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
    /// 添加快捷键绑定
    /// </summary>
    private void AddHotkeyBinding(string action, string displayName, uint key, ModifierKeys modifiers)
    {
        var vm = new HotkeyBindingViewModel(action, displayName);
        vm.LoadFromConfig(key, modifiers);
        HotkeyBindings.Add(vm);
    }

    /// <summary>
    /// 保存设置（自动生成 SaveCommand）
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        _config.SeekSeconds = SeekSeconds;
        _config.DefaultOpacity = OpacityPercent / 100.0;
        _config.EnableEdgeSnap = EnableEdgeSnap;
        _config.SnapThreshold = SnapThreshold;
        _config.PromptRecordOnExit = PromptRecordOnExit;
        _config.EnablePluginUpdateNotification = EnablePluginUpdateNotification;
        _config.EnableDebugLog = EnableDebugLog;

        // 保存快捷键（从 ViewModels 读取）
        foreach (var binding in HotkeyBindings)
        {
            switch (binding.ActionName)
            {
            case "SeekBackward":
                _config.HotkeySeekBackward = binding.Key;
                _config.HotkeySeekBackwardMod = binding.Modifiers;
                break;
            case "SeekForward":
                _config.HotkeySeekForward = binding.Key;
                _config.HotkeySeekForwardMod = binding.Modifiers;
                break;
            case "TogglePlay":
                _config.HotkeyTogglePlay = binding.Key;
                _config.HotkeyTogglePlayMod = binding.Modifiers;
                break;
            case "DecreaseOpacity":
                _config.HotkeyDecreaseOpacity = binding.Key;
                _config.HotkeyDecreaseOpacityMod = binding.Modifiers;
                break;
            case "IncreaseOpacity":
                _config.HotkeyIncreaseOpacity = binding.Key;
                _config.HotkeyIncreaseOpacityMod = binding.Modifiers;
                break;
            case "ResetOpacity":
                _config.HotkeyResetOpacity = binding.Key;
                _config.HotkeyResetOpacityMod = binding.Modifiers;
                break;
            case "ToggleClickThrough":
                _config.HotkeyToggleClickThrough = binding.Key;
                _config.HotkeyToggleClickThroughMod = binding.Modifiers;
                break;
            case "ToggleMaximize":
                _config.HotkeyToggleMaximize = binding.Key;
                _config.HotkeyToggleMaximizeMod = binding.Modifiers;
                break;
            case "DecreasePlaybackRate":
                _config.HotkeyDecreasePlaybackRate = binding.Key;
                _config.HotkeyDecreasePlaybackRateMod = binding.Modifiers;
                break;
            case "IncreasePlaybackRate":
                _config.HotkeyIncreasePlaybackRate = binding.Key;
                _config.HotkeyIncreasePlaybackRateMod = binding.Modifiers;
                break;
            case "ResetPlaybackRate":
                _config.HotkeyResetPlaybackRate = binding.Key;
                _config.HotkeyResetPlaybackRateMod = binding.Modifiers;
                break;
            case "ToggleWindowVisibility":
                _config.HotkeyToggleWindowVisibility = binding.Key;
                _config.HotkeyToggleWindowVisibilityMod = binding.Modifiers;
                break;
            case "SuspendHotkeys":
                _config.HotkeySuspendHotkeys = binding.Key;
                _config.HotkeySuspendHotkeysMod = binding.Modifiers;
                break;
            }
        }

        _configService.UpdateConfig(_config);
    }

    /// <summary>
    /// 重置为默认设置（自动生成 ResetCommand）
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        var defaultConfig = new AppConfig();
        LoadFromConfig(defaultConfig);
    }

    /// <summary>
    /// 从配置加载设置
    /// </summary>
    private void LoadFromConfig(AppConfig config)
    {
        // 重置基础设置
        SeekSeconds = config.SeekSeconds;
        OpacityPercent = (int)(config.DefaultOpacity * 100);
        EnableEdgeSnap = config.EnableEdgeSnap;
        SnapThreshold = config.SnapThreshold;
        PromptRecordOnExit = config.PromptRecordOnExit;
        EnablePluginUpdateNotification = config.EnablePluginUpdateNotification;
        EnableDebugLog = config.EnableDebugLog;

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

    /// <summary>
    /// 打开插件中心（自动生成 OpenPluginCenterCommand）
    /// </summary>
    [RelayCommand]
    private void OpenPluginCenter()
    {
        // 通过 EventBus 发布请求（解耦 PluginCenterWindow）
        _eventBus.Publish(new PluginCenterRequestedEvent());
    }

    /// <summary>
    /// 加载 Profile 列表
    /// </summary>
    public void LoadProfileList()
    {
        var profiles = _profileManager.InstalledProfiles;
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        // 选中当前 Profile
        var currentProfile = _profileManager.CurrentProfile;
        for (int i = 0; i < Profiles.Count; i++)
        {
            if (Profiles[i].Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedProfile = Profiles[i];
                break;
            }
        }
    }

    /// <summary>
    /// Profile 选择变化（自动生成的方法）
    /// </summary>
    partial void OnSelectedProfileChanged(GameProfile? value)
    {
        if (value != null)
        {
            var currentProfile = _profileManager.CurrentProfile;
            if (!value.Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                _profileManager.SwitchProfile(value.Id);
            }
        }
    }

    /// <summary>
    /// 取消订阅 Profile（自动生成 UnsubscribeProfileCommand）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUnsubscribeProfile))]
    private void UnsubscribeProfile()
    {
        if (SelectedProfile == null)
            return;

        if (SelectedProfile.Id.Equals("default", StringComparison.OrdinalIgnoreCase))
            return; // 不能删除默认 Profile

        var result = _profileManager.UnsubscribeProfile(SelectedProfile.Id);
        if (result.IsSuccess)
        {
            LoadProfileList();
        }
    }

    /// <summary>
    /// 是否可以取消订阅
    /// </summary>
    private bool CanUnsubscribeProfile() => SelectedProfile != null &&
                                            !SelectedProfile.Id.Equals("default", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 打开配置文件夹（自动生成 OpenConfigFolderCommand）
    /// </summary>
    [RelayCommand]
    private void OpenConfigFolder()
    {
        // 通过事件通知 Code-behind，传递路径参数
        var path = _profileManager.DataDirectory;
        OpenConfigFolderRequested?.Invoke(this, path);
    }

    /// <summary>
    /// 请求打开配置文件夹事件（参数：配置目录路径）
    /// </summary>
    public event EventHandler<string>? OpenConfigFolderRequested;

    /// <summary>
    /// 插件中心关闭后请求刷新 Profile 列表
    /// </summary>
    public void RefreshProfileList()
    {
        _profileManager.ReloadProfiles();
        LoadProfileList();
    }
}
}
