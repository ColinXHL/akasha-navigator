using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Views.Pages
{
    /// <summary>
    /// Profile 市场页面 - 浏览和安装市场 Profile
    /// </summary>
    public partial class ProfileMarketPage : System.Windows.Controls.UserControl
    {
        private readonly ProfileMarketPageViewModel _viewModel;

        /// <summary>
        /// DI容器注入的构造函数
        /// </summary>
        public ProfileMarketPage(ProfileMarketPageViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;
            Loaded += ProfileMarketPage_Loaded;

            // 订阅 ViewModel 的事件
            _viewModel.ManageSourcesRequested += OnManageSourcesRequested;
            _viewModel.ShowProfileDetailsRequested += OnShowProfileDetailsRequested;
            _viewModel.UninstallProfileRequested += OnUninstallProfileRequested;
        }

        private void ProfileMarketPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 首次加载时获取数据
            _ = _viewModel.LoadProfilesAsync();
        }

        /// <summary>
        /// 处理订阅源管理请求
        /// </summary>
        private void OnManageSourcesRequested(object? sender, EventArgs e)
        {
            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreateSubscriptionSourceDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                // 订阅源变更后刷新列表
                _ = _viewModel.LoadProfilesAsync();
            }
        }

        /// <summary>
        /// 处理显示 Profile 详情请求
        /// </summary>
        private void OnShowProfileDetailsRequested(object? sender, MarketplaceProfileViewModel vm)
        {
            var dialogViewModel = App.Services.GetRequiredService<MarketplaceProfileDetailDialogViewModel>();
            var dialog = new MarketplaceProfileDetailDialog(dialogViewModel, vm.Profile);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true && dialog.ShouldInstall)
            {
                _ = _viewModel.InstallCommand.ExecuteAsync(vm);
            }
        }

        /// <summary>
        /// 处理卸载 Profile 请求（显示卸载对话框）
        /// </summary>
        private void OnUninstallProfileRequested(object? sender, ProfileUninstallEventArgs args)
        {
            var pluginItems = new List<PluginUninstallItem>();

            foreach (var pluginId in args.UniquePluginIds)
            {
                var pluginInfo = _viewModel.GetInstalledPluginInfo(pluginId);
                pluginItems.Add(new PluginUninstallItem
                {
                    PluginId = pluginId,
                    Name = pluginInfo?.Name ?? pluginId,
                    Description = pluginInfo?.Description ?? string.Empty,
                    IsSelected = true // 默认选中
                });
            }

            var dialogFactory = App.Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreatePluginUninstallDialog(args.ProfileName, pluginItems);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() != true || !dialog.Confirmed)
            {
                // 用户取消
                args.Confirmed = false;
                return;
            }

            args.Confirmed = true;
            args.SelectedPluginIds = dialog.SelectedPluginIds;
        }
    }
}
