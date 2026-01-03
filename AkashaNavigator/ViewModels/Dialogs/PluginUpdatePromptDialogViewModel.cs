using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 插件更新提示对话框的 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class PluginUpdatePromptDialogViewModel : ObservableObject
    {
        private readonly IConfigService _configService;

        /// <summary>
        /// 有更新的插件列表
        /// </summary>
        public List<UpdateCheckResult> UpdatesAvailable { get; private set; } = new();

        /// <summary>
        /// 更新提示消息（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _updateMessage = string.Empty;

        /// <summary>
        /// 不再提示复选框状态（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _dontShowAgain;

        /// <summary>
        /// 对话框结果
        /// </summary>
        public PluginUpdatePromptResult Result { get; private set; } = PluginUpdatePromptResult.Cancel;

        /// <summary>
        /// 请求关闭对话框事件（参数为对话框结果和 DialogModelValue）
        /// </summary>
        public event EventHandler<PluginUpdatePromptResult>? RequestClose;

        /// <summary>
        /// 构造函数 - 只接收服务依赖
        /// </summary>
        public PluginUpdatePromptDialogViewModel(IConfigService configService)
        {
            _configService = configService ?? throw new System.ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// 初始化方法 - 接收运行时参数
        /// </summary>
        public void Initialize(List<UpdateCheckResult> updates)
        {
            UpdatesAvailable = updates ?? throw new System.ArgumentNullException(nameof(updates));

            // 设置提示文字
            UpdateMessage = $"发现 {updates.Count} 个插件有可用更新。\n是否立即更新？";
        }

        /// <summary>
        /// 打开插件中心（自动生成 OpenPluginCenterCommand）
        /// </summary>
        [RelayCommand]
        private void OpenPluginCenter()
        {
            SaveDontShowAgainSetting();
            Result = PluginUpdatePromptResult.OpenPluginCenter;
            RequestClose?.Invoke(this, Result);
        }

        /// <summary>
        /// 一键更新（自动生成 UpdateAllCommand）
        /// </summary>
        [RelayCommand]
        private void UpdateAll()
        {
            SaveDontShowAgainSetting();
            Result = PluginUpdatePromptResult.UpdateAll;
            RequestClose?.Invoke(this, Result);
        }

        /// <summary>
        /// 取消（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            SaveDontShowAgainSetting();
            Result = PluginUpdatePromptResult.Cancel;
            RequestClose?.Invoke(this, Result);
        }

        /// <summary>
        /// 保存不再提示设置
        /// </summary>
        private void SaveDontShowAgainSetting()
        {
            if (DontShowAgain)
            {
                var config = _configService.Config;
                config.EnablePluginUpdateNotification = false;
                _configService.Save();
            }
        }
    }
}

/// <summary>
/// 插件更新提示对话框的操作结果
/// </summary>
public enum PluginUpdatePromptResult
{
    /// <summary>取消</summary>
    Cancel,
    /// <summary>打开插件中心</summary>
    OpenPluginCenter,
    /// <summary>一键更新</summary>
    UpdateAll
}
