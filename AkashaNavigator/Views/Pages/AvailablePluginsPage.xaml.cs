using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Views.Pages
{
    /// <summary>
    /// 可用插件页面 - 显示所有内置插件（包括已安装和未安装）
    /// </summary>
    public partial class AvailablePluginsPage : UserControl
    {
        private readonly AvailablePluginsPageViewModel _viewModel;

        // DI 构造函数
        public AvailablePluginsPage(AvailablePluginsPageViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;

            // 订阅 ViewModel 的事件
            _viewModel.RefreshRequested += OnRefreshRequested;
            _viewModel.UninstallRequested += OnUninstallRequested;

            Loaded += AvailablePluginsPage_Loaded;
        }

        private void AvailablePluginsPage_Loaded(object sender, RoutedEventArgs e)
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
        /// 处理刷新请求（UI 逻辑：通知父窗口刷新）
        /// </summary>
        private void OnRefreshRequested(object? sender, System.EventArgs e)
        {
            // 通知父窗口刷新
            if (Window.GetWindow(this) is Views.Windows.PluginCenterWindow centerWindow)
            {
                centerWindow.RefreshCurrentPage();
            }
        }

        /// <summary>
        /// 处理卸载请求（UI 逻辑：显示确认对话框）
        /// </summary>
        private void OnUninstallRequested(object? sender, AvailablePluginItemModel plugin)
        {
            if (plugin == null)
                return;

            // 显示卸载确认对话框
            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateUninstallConfirmDialog(plugin.Id, plugin.Name);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.UninstallSucceeded)
            {
                // 更新插件状态
                plugin.IsInstalled = false;

                // 通知父窗口刷新
                if (Window.GetWindow(this) is Views.Windows.PluginCenterWindow centerWindow)
                {
                    centerWindow.RefreshCurrentPage();
                }
            }
        }
    }
}
