using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 插件卸载选择对话框 ViewModel
    /// 在卸载 Profile 时显示唯一插件列表供用户选择
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class PluginUninstallDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 插件列表
        /// </summary>
        public ObservableCollection<PluginUninstallItem> Plugins { get; }

        /// <summary>
        /// Profile 名称（用于显示）
        /// </summary>
        public string ProfileName { get; }

        /// <summary>
        /// 用户是否确认卸载
        /// </summary>
        public bool Confirmed { get; private set; }

        /// <summary>
        /// 获取用户选择要卸载的插件 ID 列表
        /// </summary>
        public List<string> SelectedPluginIds => Plugins.Where(p => p.IsSelected).Select(p => p.PluginId).ToList();

        /// <summary>
        /// 请求关闭对话框事件（参数：是否确认）
        /// </summary>
        public event EventHandler<bool>? RequestClose;

        /// <summary>
        /// 创建插件卸载选择对话框 ViewModel
        /// </summary>
        /// <param name="profileName">Profile 名称（用于显示）</param>
        /// <param name="plugins">唯一插件列表</param>
        public PluginUninstallDialogViewModel(string profileName, IEnumerable<PluginUninstallItem> plugins)
        {
            ProfileName = profileName ?? string.Empty;
            Plugins = new ObservableCollection<PluginUninstallItem>(plugins);
        }

        /// <summary>
        /// 全选命令（自动生成 SelectAllCommand）
        /// </summary>
        [RelayCommand]
        private void SelectAll()
        {
            foreach (var plugin in Plugins)
            {
                plugin.IsSelected = true;
            }
        }

        /// <summary>
        /// 全不选命令（自动生成 SelectNoneCommand）
        /// </summary>
        [RelayCommand]
        private void SelectNone()
        {
            foreach (var plugin in Plugins)
            {
                plugin.IsSelected = false;
            }
        }

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Confirmed = false;
            RequestClose?.Invoke(this, false);
        }

        /// <summary>
        /// 确认卸载命令（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            Confirmed = true;
            RequestClose?.Invoke(this, true);
        }
    }
}
