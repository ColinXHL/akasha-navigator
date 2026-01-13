using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Dialogs
{
/// <summary>
/// Profile 编辑对话框 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class ProfileEditDialogViewModel : ObservableObject
{
    private readonly IProfileManager _profileManager;

    // 原始值跟踪字段（用于变更检测）
    private string _originalName = string.Empty;
    private string _originalIcon = string.Empty;
    private string _originalDefaultUrl = string.Empty;
    private int _originalSeekSeconds = 5;

    private string _profileId = string.Empty;
    private GameProfile? _profile;

    /// <summary>
    /// 可用图标列表
    /// </summary>
    public ObservableCollection<string> AvailableIcons { get; } = new();

#region 基本信息属性

    /// <summary>
    /// Profile 名称
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _profileName = string.Empty;

    /// <summary>
    /// 选中的图标
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _selectedIcon = "📦";

#endregion

#region 默认设置属性

    /// <summary>
    /// 默认 URL
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _defaultUrl = string.Empty;

    /// <summary>
    /// 快进/倒退秒数 (1 - 60)
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _seekSeconds = 5;

#endregion

#region 验证属性

    /// <summary>
    /// URL 验证错误消息
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _urlError;

    /// <summary>
    /// 是否存在验证错误
    /// </summary>
    public bool HasValidationErrors => !string.IsNullOrEmpty(UrlError);

#endregion

    /// <summary>
    /// 错误消息
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 是否显示错误消息
    /// </summary>
    public bool ShowError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// 对话框结果
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// 请求关闭事件
    /// </summary>
    public event EventHandler<bool?>? RequestClose;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ProfileEditDialogViewModel(IProfileManager profileManager)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        LoadIcons();
    }

    /// <summary>
    /// 初始化方法
    /// </summary>
    public void Initialize(GameProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profileId = profile.Id;

        // 基本信息
        _originalName = profile.Name;
        _originalIcon = profile.Icon;
        ProfileName = profile.Name;
        SelectedIcon = profile.Icon;

        // 默认设置
        var defaults = profile.Defaults;
        _originalDefaultUrl = defaults?.Url ?? string.Empty;
        _originalSeekSeconds = defaults?.SeekSeconds ?? 5;

        DefaultUrl = _originalDefaultUrl;
        SeekSeconds = _originalSeekSeconds;

        ClearValidationErrors();
    }

    private void LoadIcons()
    {
        var icons = _profileManager.ProfileIcons;
        AvailableIcons.Clear();
        foreach (var icon in icons)
        {
            AvailableIcons.Add(icon);
        }
    }

#region 验证方法

    private void ValidateUrl()
    {
        var url = DefaultUrl?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            UrlError = null;
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            UrlError = "URL 格式无效";
        }
        else
        {
            UrlError = null;
        }
    }

    private void ClearValidationErrors()
    {
        UrlError = null;
    }

#endregion

#region 属性变更处理

    partial void OnProfileNameChanged(string value) => ClearError();
    partial void OnDefaultUrlChanged(string value) => ValidateUrl();

#endregion

#region 命令

    /// <summary>
    /// 保存 Profile
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!ValidateInput())
            return;

        var updateData = new ProfileUpdateData {
            Name = ProfileName.Trim(), Icon = SelectedIcon,
            Defaults = new ProfileDefaults { Url = string.IsNullOrWhiteSpace(DefaultUrl) ? null : DefaultUrl.Trim(),
                                             SeekSeconds = SeekSeconds }
        };

        var success = _profileManager.UpdateProfile(_profileId, updateData);

        if (success)
        {
            DialogResult = true;
            RequestClose?.Invoke(this, true);
        }
        else
        {
            SetError("保存失败");
        }
    }

    private bool CanSave()
    {
        if (HasValidationErrors)
            return false;
        if (string.IsNullOrWhiteSpace(ProfileName?.Trim()))
            return false;
        return HasChanges();
    }

    private bool HasChanges()
    {
        if (ProfileName?.Trim() != _originalName)
            return true;
        if (SelectedIcon != _originalIcon)
            return true;
        if ((DefaultUrl?.Trim() ?? string.Empty) != _originalDefaultUrl)
            return true;
        if (SeekSeconds != _originalSeekSeconds)
            return true;
        return false;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand]
    private void Close()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, null);
    }

#endregion

#region 辅助方法

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(ProfileName?.Trim()))
        {
            SetError("Profile 名称不能为空");
            return false;
        }

        ValidateUrl();

        if (HasValidationErrors)
            return false;

        ClearError();
        return true;
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        OnPropertyChanged(nameof(ShowError));
    }

    private void ClearError()
    {
        ErrorMessage = null;
        OnPropertyChanged(nameof(ShowError));
    }

#endregion
}
}
