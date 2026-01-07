using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Pages;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// 插件中心窗口 - 统一管理所有插件的安装、卸载和配置
/// </summary>
public partial class PluginCenterWindow : AnimatedWindow
{
    private MyProfilesPage _myProfilesPage = null!;
    private ProfileMarketPage _profileMarketPage = null!;
    private InstalledPluginsPage _installedPluginsPage = null!;
    private AvailablePluginsPage _availablePluginsPage = null!;

    public PluginCenterWindow()
    {
        InitializeComponent();

        // 通过 DI 创建 Pages 并添加到 ContentArea
        LoadPages();
    }

    /// <summary>
    /// 加载所有 Pages
    /// </summary>
    private void LoadPages()
    {
        var serviceProvider = App.Services;

        _myProfilesPage = serviceProvider.GetRequiredService<MyProfilesPage>();
        _profileMarketPage = serviceProvider.GetRequiredService<ProfileMarketPage>();
        _installedPluginsPage = serviceProvider.GetRequiredService<InstalledPluginsPage>();
        _availablePluginsPage = serviceProvider.GetRequiredService<AvailablePluginsPage>();

        ContentArea.Children.Add(_myProfilesPage);
        ContentArea.Children.Add(_profileMarketPage);
        ContentArea.Children.Add(_installedPluginsPage);
        ContentArea.Children.Add(_availablePluginsPage);

        // 默认显示我的 Profile 页面
        ShowMyProfiles();
    }

    /// <summary>
    /// 显示我的 Profile 页面
    /// </summary>
    private void ShowMyProfiles()
    {
        _myProfilesPage.Visibility = Visibility.Visible;
        _profileMarketPage.Visibility = Visibility.Collapsed;
        _installedPluginsPage.Visibility = Visibility.Collapsed;
        _availablePluginsPage.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击切换最大化/还原
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 导航按钮切换
    /// </summary>
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton)
            return;

        // 防止在 InitializeComponent 期间触发（控件尚未初始化）
        if (_installedPluginsPage == null)
            return;

        // 隐藏所有页面
        _installedPluginsPage.Visibility = Visibility.Collapsed;
        _availablePluginsPage.Visibility = Visibility.Collapsed;
        _myProfilesPage.Visibility = Visibility.Collapsed;
        _profileMarketPage.Visibility = Visibility.Collapsed;

        // 根据选中的导航按钮显示对应页面
        if (radioButton == NavInstalledPlugins)
        {
            _installedPluginsPage.Visibility = Visibility.Visible;
            _installedPluginsPage.CheckAndRefreshPluginList();
        }
        else if (radioButton == NavAvailablePlugins)
        {
            _availablePluginsPage.Visibility = Visibility.Visible;
            _availablePluginsPage.RefreshPluginList();
        }
        else if (radioButton == NavMyProfiles)
        {
            _myProfilesPage.Visibility = Visibility.Visible;
            _myProfilesPage.RefreshProfileList();
        }
        else if (radioButton == NavProfileMarket)
        {
            _profileMarketPage.Visibility = Visibility.Visible;
            var vm = _profileMarketPage.DataContext as ViewModels.Pages.ProfileMarketPageViewModel;
            if (vm != null)
            {
                _ = vm.LoadProfilesAsync();
            }
        }
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    public void RefreshCurrentPage()
    {
        if (NavInstalledPlugins.IsChecked == true)
        {
            _installedPluginsPage?.CheckAndRefreshPluginList();
        }
        else if (NavAvailablePlugins.IsChecked == true)
        {
            _availablePluginsPage?.RefreshPluginList();
        }
    }

    /// <summary>
    /// 导航到已安装插件页面
    /// </summary>
    public void NavigateToInstalledPlugins()
    {
        NavInstalledPlugins.IsChecked = true;
    }

    /// <summary>
    /// 导航到可用插件页面
    /// </summary>
    public void NavigateToAvailablePlugins()
    {
        NavAvailablePlugins.IsChecked = true;
    }
}
}
