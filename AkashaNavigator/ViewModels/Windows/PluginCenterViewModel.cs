using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 插件中心窗口 ViewModel
/// 只负责页面导航；页面刷新由持有实际 Page 实例的窗口负责。
/// </summary>
public partial class PluginCenterViewModel : ObservableObject
{
    /// <summary>
    /// 当前显示的页面类型（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private PluginCenterPageType _currentPage = PluginCenterPageType.MyProfiles;

    /// <summary>
    /// 导航到我的配置页面（自动生成 NavigateToMyProfilesCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToMyProfiles()
    {
        CurrentPage = PluginCenterPageType.MyProfiles;
    }

    /// <summary>
    /// 导航到配置市场页面（自动生成 NavigateToProfileMarketCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToProfileMarket()
    {
        CurrentPage = PluginCenterPageType.ProfileMarket;
    }

    /// <summary>
    /// 导航到已安装插件页面（自动生成 NavigateToInstalledPluginsCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToInstalledPlugins()
    {
        CurrentPage = PluginCenterPageType.InstalledPlugins;
    }

    /// <summary>
    /// 导航到可用插件页面（自动生成 NavigateToAvailablePluginsCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToAvailablePlugins()
    {
        CurrentPage = PluginCenterPageType.AvailablePlugins;
    }
}

/// <summary>
/// 插件中心页面类型
/// </summary>
public enum PluginCenterPageType
{
    MyProfiles,
    ProfileMarket,
    InstalledPlugins,
    AvailablePlugins
}
}
