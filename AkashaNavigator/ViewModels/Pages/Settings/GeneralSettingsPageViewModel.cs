using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 通用设置页面 ViewModel
/// 包含：Profile 选择、基础设置（快进/倒退秒数、透明度）、打开文件夹、插件中心
/// </summary>
public partial class GeneralSettingsPageViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
    private readonly IWindowStateService _windowStateService;
    private readonly IEventBus _eventBus;
    private AppConfig _config;

    /// <summary>
    /// 可用 Profile 列表
    /// </summary>
    public ObservableCollection<GameProfile> Profiles { get; } = new();

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
    /// 当前选中的 Profile（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private GameProfile? _selectedProfile;

    public GeneralSettingsPageViewModel(IConfigService configService, IProfileManager profileManager,
                                        IWindowStateService windowStateService, IEventBus eventBus)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        _config = _configService.Config;

        // 订阅透明度变化事件（来自快捷键操作）
        _eventBus.Subscribe<OpacityChangedEvent>(OnOpacityChanged);

        LoadSettings();
        LoadProfileList();
    }

    /// <summary>
    /// 处理透明度变化事件（来自 PlayerWindow）
    /// </summary>
    private void OnOpacityChanged(OpacityChangedEvent e)
    {
        // 忽略来自设置界面的事件，避免循环
        if (e.Source == OpacityChangeSource.Settings)
            return;

        OpacityPercent = (int)(e.Opacity * 100);
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        SeekSeconds = _config.SeekSeconds;

        // 通过事件查询 PlayerWindow 的当前透明度
        double currentOpacity = 1.0;
        _eventBus.Publish(new OpacityQueryEvent { Callback = opacity => currentOpacity = opacity });
        OpacityPercent = (int)(currentOpacity * 100);
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
                // 切换 Profile 后清除缓存并重新加载透明度
                _windowStateService.ClearCache();
                LoadSettings();
            }
        }
    }

    /// <summary>
    /// 刷新 Profile 列表（从插件中心返回后调用）
    /// </summary>
    public void RefreshProfileList()
    {
        _profileManager.ReloadProfiles();
        LoadProfileList();
    }

    /// <summary>
    /// 刷新设置（设置窗口打开时调用）
    /// </summary>
    public void RefreshSettings()
    {
        LoadSettings();
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.SeekSeconds = SeekSeconds;

        // 透明度保存到 WindowState
        var opacity = OpacityPercent / 100.0;
        _windowStateService.Update(state => state.Opacity = opacity);

        // 发布事件通知 PlayerWindow 更新透明度
        _eventBus.Publish(new OpacityChangedEvent { Opacity = opacity, Source = OpacityChangeSource.Settings });
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        SeekSeconds = config.SeekSeconds;

        // 通过事件查询当前透明度
        double currentOpacity = 1.0;
        _eventBus.Publish(new OpacityQueryEvent { Callback = opacity => currentOpacity = opacity });
        OpacityPercent = (int)(currentOpacity * 100);
    }
}
