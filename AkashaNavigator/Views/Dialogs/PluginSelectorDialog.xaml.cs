using System.Windows;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 插件选择对话框 - 从已安装插件中选择添加到 Profile
    /// </summary>
    public partial class PluginSelectorDialog : AnimatedWindow
    {
        private readonly PluginSelectorDialogViewModel _viewModel;

        /// <summary>
        /// 添加的插件数量
        /// </summary>
        public int AddedCount => _viewModel.AddedCount;

        public PluginSelectorDialog(PluginSelectorDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // 订阅 ViewModel 的关闭请求
            _viewModel.RequestClose += OnRequestClose;
        }

        /// <summary>
        /// 初始化插件列表（在设置 DataContext 后调用）
        /// </summary>
        public void InitializePlugins(System.Collections.Generic.List<Models.Plugin.InstalledPluginInfo> availablePlugins, string profileId)
        {
            _viewModel.Initialize(profileId, availablePlugins);
        }

        #region UI Event Handlers

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            DialogResult = dialogResult;
            CloseWithAnimation();
        }

        #endregion
    }
}
