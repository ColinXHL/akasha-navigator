using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Pages.Settings;

/// <summary>
/// 通用设置页面 ViewModel
/// 包含：Profile 选择、基础设置（快进/倒退秒数、默认透明度）、打开文件夹、插件中心
/// </summary>
public partial class GeneralSettingsPageViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
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

    public GeneralSettingsPageViewModel(IConfigService configService, IProfileManager profileManager)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

        _config = _configService.Config;
        LoadSettings();
        LoadProfileList();
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        SeekSeconds = _config.SeekSeconds;
        OpacityPercent = (int)(_config.DefaultOpacity * 100);
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
    /// 刷新 Profile 列表（从插件中心返回后调用）
    /// </summary>
    public void RefreshProfileList()
    {
        _profileManager.ReloadProfiles();
        LoadProfileList();
    }

    /// <summary>
    /// 保存设置到配置对象
    /// </summary>
    public void SaveSettings(AppConfig config)
    {
        config.SeekSeconds = SeekSeconds;
        config.DefaultOpacity = OpacityPercent / 100.0;
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    public void ResetSettings(AppConfig config)
    {
        SeekSeconds = config.SeekSeconds;
        OpacityPercent = (int)(config.DefaultOpacity * 100);
    }
}
