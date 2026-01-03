using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 欢迎对话框 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class WelcomeDialogViewModel : ObservableObject
    {
        /// <summary>
        /// GitHub 仓库 URL
        /// </summary>
        public const string GitHubUrl = "https://github.com/ColinXHL/akasha-navigator";

        /// <summary>
        /// GitHub 仓库显示名称
        /// </summary>
        [ObservableProperty]
        private string _gitHubRepositoryName = "ColinXHL/akasha-navigator";

        /// <summary>
        /// 对话框请求关闭事件
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 构造函数
        /// </summary>
        public WelcomeDialogViewModel()
        {
        }

        /// <summary>
        /// 打开 GitHub 仓库命令（自动生成 OpenGitHubCommand）
        /// </summary>
        [RelayCommand]
        private void OpenGitHub()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = GitHubUrl, UseShellExecute = true });
            }
            catch
            {
                // 忽略打开链接失败
            }
        }

        /// <summary>
        /// 开始使用命令（自动生成 StartCommand）
        /// </summary>
        [RelayCommand]
        private void Start()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 关闭命令（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
