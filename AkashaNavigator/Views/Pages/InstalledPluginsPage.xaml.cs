using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Views.Pages
{
    /// <summary>
    /// 已安装插件页面 - 显示全局插件库中的所有插件
    /// </summary>
    public partial class InstalledPluginsPage : UserControl
    {
        private readonly InstalledPluginsPageViewModel _viewModel;
        private readonly IPluginLibrary _pluginLibrary;

        // DI 构造函数
        public InstalledPluginsPage(
            InstalledPluginsPageViewModel viewModel,
            IPluginLibrary pluginLibrary)
        {
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            _pluginLibrary = pluginLibrary ?? throw new System.ArgumentNullException(nameof(pluginLibrary));
            InitializeComponent();

            DataContext = _viewModel;

            // 订阅 ViewModel 的事件
            _viewModel.AddToProfileRequested += OnAddToProfileRequested;
            _viewModel.UninstallRequested += OnUninstallRequested;

            Loaded += InstalledPluginsPage_Loaded;
        }

        private void InstalledPluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 委托给 ViewModel 的 OnLoaded 方法
            _viewModel.OnLoaded();
        }

        /// <summary>
        /// 刷新插件列表（公共方法）
        /// </summary>
        public void RefreshPluginList()
        {
            _viewModel.RefreshPluginList();
        }

        /// <summary>
        /// 检查更新并刷新插件列表（公共方法）
        /// </summary>
        public void CheckAndRefreshPluginList()
        {
            _viewModel.CheckAndRefreshPluginList();
        }

        /// <summary>
        /// 处理添加到 Profile 请求（UI 逻辑：显示对话框）
        /// </summary>
        private void OnAddToProfileRequested(object? sender, string? pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;

            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateProfileSelectorDialog(pluginId);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                RefreshPluginList();
            }
        }

        /// <summary>
        /// 处理卸载请求（UI 逻辑：显示确认对话框）
        /// </summary>
        private void OnUninstallRequested(object? sender, string? pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return;

            // 获取插件名称用于显示
            var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
            var pluginName = pluginInfo?.Name ?? pluginId;

            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateUninstallConfirmDialog(pluginId, pluginName);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.UninstallSucceeded)
            {
                RefreshPluginList();
            }
        }
    }
}
