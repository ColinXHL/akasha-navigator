using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 插件选择对话框 ViewModel - 从已安装插件中选择添加到 Profile
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class PluginSelectorDialogViewModel : ObservableObject
    {
        private readonly IPluginAssociationManager _pluginAssociationManager;

        /// <summary>
        /// 可选插件列表
        /// </summary>
        public ObservableCollection<PluginSelectorItem> Plugins { get; } = new();

        /// <summary>
        /// 选中的插件数量（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private int _selectedCount;

        /// <summary>
        /// 确认按钮文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _confirmButtonText = "添加选中的插件";

        /// <summary>
        /// 对话框结果
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 请求关闭事件
        /// </summary>
        public event EventHandler<bool?>? RequestClose;

        /// <summary>
        /// 添加的插件数量
        /// </summary>
        public int AddedCount { get; private set; }

        /// <summary>
        /// Profile ID
        /// </summary>
        public string ProfileId { get; private set; } = string.Empty;

        /// <summary>
        /// 构造函数 - 只接收服务依赖
        /// </summary>
        public PluginSelectorDialogViewModel(IPluginAssociationManager pluginAssociationManager)
        {
            _pluginAssociationManager = pluginAssociationManager
                ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        }

        /// <summary>
        /// 初始化方法 - 接收运行时参数
        /// </summary>
        public void Initialize(string profileId, System.Collections.Generic.List<Models.Plugin.InstalledPluginInfo> availablePlugins)
        {
            ProfileId = profileId ?? string.Empty;

            Plugins.Clear();
            foreach (var plugin in availablePlugins)
            {
                var item = new PluginSelectorItem
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Version = plugin.Version,
                    Description = plugin.Description,
                    IsSelected = false
                };

                // 订阅选择变化
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PluginSelectorItem.IsSelected))
                    {
                        UpdateSelectionState();
                    }
                };

                Plugins.Add(item);
            }

            UpdateSelectionState();
        }

        /// <summary>
        /// 更新选择状态
        /// </summary>
        private void UpdateSelectionState()
        {
            SelectedCount = Plugins.Count(p => p.IsSelected);
            ConfirmButtonText = SelectedCount > 0
                ? $"添加选中的 {SelectedCount} 个插件"
                : "添加选中的插件";
        }

        /// <summary>
        /// 确认命令（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            var selectedPlugins = Plugins.Where(p => p.IsSelected).Select(p => p.Id).ToList();

            if (selectedPlugins.Count == 0)
            {
                DialogResult = false;
                RequestClose?.Invoke(this, false);
                return;
            }

            // 添加到 Profile
            AddedCount = _pluginAssociationManager.AddPluginsToProfile(selectedPlugins, ProfileId);
            DialogResult = AddedCount > 0;
            RequestClose?.Invoke(this, DialogResult);
        }

        /// <summary>
        /// 是否可以确认（至少选中一个插件）
        /// </summary>
        private bool CanConfirm() => SelectedCount > 0;

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke(this, false);
        }
    }
}
