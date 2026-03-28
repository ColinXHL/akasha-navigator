using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Dialogs
{
/// <summary>
/// Profile 更新提示对话框的 ViewModel。
/// </summary>
public partial class ProfileUpdatePromptDialogViewModel : ObservableObject
{
    private readonly IConfigService _configService;

    public List<ProfileUpdateCheckResult> UpdatesAvailable { get; private set; } = new();

    [ObservableProperty]
    private string _updateMessage = string.Empty;

    [ObservableProperty]
    private bool _dontShowAgain;

    public ProfileUpdatePromptResult Result { get; private set; } = ProfileUpdatePromptResult.Cancel;

    public event EventHandler<ProfileUpdatePromptResult>? RequestClose;

    public ProfileUpdatePromptDialogViewModel(IConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public void Initialize(List<ProfileUpdateCheckResult> updates)
    {
        UpdatesAvailable = updates ?? throw new ArgumentNullException(nameof(updates));
        UpdateMessage = $"发现 {updates.Count} 个 Profile 有可用更新。\n是否立即更新？";
    }

    [RelayCommand]
    private void OpenPluginCenter()
    {
        SaveDontShowAgainSetting();
        Result = ProfileUpdatePromptResult.OpenPluginCenter;
        RequestClose?.Invoke(this, Result);
    }

    [RelayCommand]
    private void UpdateAll()
    {
        SaveDontShowAgainSetting();
        Result = ProfileUpdatePromptResult.UpdateAll;
        RequestClose?.Invoke(this, Result);
    }

    [RelayCommand]
    private void Cancel()
    {
        SaveDontShowAgainSetting();
        Result = ProfileUpdatePromptResult.Cancel;
        RequestClose?.Invoke(this, Result);
    }

    private void SaveDontShowAgainSetting()
    {
        if (!DontShowAgain)
            return;

        var config = _configService.Config;
        config.EnablePluginUpdateNotification = false;
        _configService.Save();
    }
}

public enum ProfileUpdatePromptResult
{
    Cancel,
    OpenPluginCenter,
    UpdateAll
}
}
