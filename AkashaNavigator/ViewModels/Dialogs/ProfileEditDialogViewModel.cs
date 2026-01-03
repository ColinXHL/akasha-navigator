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
        private string _originalName = string.Empty;
        private string _originalIcon = string.Empty;
        private string _profileId = string.Empty;
        private GameProfile? _profile;

        /// <summary>
        /// 可用图标列表
        /// </summary>
        public ObservableCollection<string> AvailableIcons { get; } = new();

        /// <summary>
        /// Profile 名称（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _profileName = string.Empty;

        /// <summary>
        /// 选中的图标（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _selectedIcon = "📦";

        /// <summary>
        /// 错误消息（自动生成属性和通知）
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
        /// 构造函数 - 只接收服务依赖
        /// </summary>
        public ProfileEditDialogViewModel(IProfileManager profileManager)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            LoadIcons();
        }

        /// <summary>
        /// 初始化方法 - 接收运行时参数
        /// </summary>
        public void Initialize(GameProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            _profileId = profile.Id;
            _originalName = profile.Name;
            _originalIcon = profile.Icon;

            // 初始化值
            ProfileName = profile.Name;
            SelectedIcon = profile.Icon;
        }

        /// <summary>
        /// 加载图标列表
        /// </summary>
        private void LoadIcons()
        {
            var icons = _profileManager.ProfileIcons;
            AvailableIcons.Clear();
            foreach (var icon in icons)
            {
                AvailableIcons.Add(icon);
            }
        }

        /// <summary>
        /// 保存 Profile（自动生成 SaveCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            // 验证输入
            if (!ValidateInput())
            {
                return;
            }

            // 获取输入值
            var newName = ProfileName.Trim();
            var newIcon = SelectedIcon;

            // 更新 Profile
            var success = _profileManager.UpdateProfile(_profileId, newName, newIcon);

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

        /// <summary>
        /// 是否可以保存（名称不为空且内容有变化）
        /// </summary>
        private bool CanSave()
        {
            var name = ProfileName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // 检查是否有变化
            return name != _originalName || SelectedIcon != _originalIcon;
        }

        /// <summary>
        /// 取消（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke(this, false);
        }

        /// <summary>
        /// 关闭窗口（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
            RequestClose?.Invoke(this, null);
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            var name = ProfileName?.Trim();

            // 检查名称是否为空
            if (string.IsNullOrWhiteSpace(name))
            {
                SetError("Profile 名称不能为空");
                return false;
            }

            // 清除错误
            ClearError();
            return true;
        }

        /// <summary>
        /// 设置错误消息
        /// </summary>
        private void SetError(string message)
        {
            ErrorMessage = message;
            OnPropertyChanged(nameof(ShowError));
        }

        /// <summary>
        /// 清除错误消息
        /// </summary>
        private void ClearError()
        {
            ErrorMessage = null;
            OnPropertyChanged(nameof(ShowError));
        }

        /// <summary>
        /// Profile 名称或图标变化时调用
        /// </summary>
        partial void OnProfileNameChanged(string value)
        {
            ClearError();
        }

        partial void OnSelectedIconChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
