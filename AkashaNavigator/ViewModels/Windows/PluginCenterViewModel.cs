using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.ViewModels.Pages;

namespace AkashaNavigator.ViewModels.Windows
{
/// <summary>
/// 插件中心窗口 ViewModel
/// 遵循 MVVM 原则：ViewModel 只依赖 PageViewModel，不直接引用 View
/// </summary>
public partial class PluginCenterViewModel : ObservableObject
{
    private readonly MyProfilesPageViewModel _myProfilesPageVM;
    private readonly ProfileMarketPageViewModel _profileMarketPageVM;
    private readonly InstalledPluginsPageViewModel _installedPluginsPageVM;
    private readonly AvailablePluginsPageViewModel _availablePluginsPageVM;

    /// <summary>
    /// 当前显示的页面类型（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private PluginCenterPageType _currentPage = PluginCenterPageType.MyProfiles;

    public PluginCenterViewModel(MyProfilesPageViewModel myProfilesPageVM,
                                 ProfileMarketPageViewModel profileMarketPageVM,
                                 InstalledPluginsPageViewModel installedPluginsPageVM,
                                 AvailablePluginsPageViewModel availablePluginsPageVM)
    {
        _myProfilesPageVM = myProfilesPageVM ?? throw new ArgumentNullException(nameof(myProfilesPageVM));
        _profileMarketPageVM = profileMarketPageVM ?? throw new ArgumentNullException(nameof(profileMarketPageVM));
        _installedPluginsPageVM =
            installedPluginsPageVM ?? throw new ArgumentNullException(nameof(installedPluginsPageVM));
        _availablePluginsPageVM =
            availablePluginsPageVM ?? throw new ArgumentNullException(nameof(availablePluginsPageVM));
    }

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

    /// <summary>
    /// CurrentPage 属性变化时的处理（由 CommunityToolkit.Mvvm 自动调用）
    /// </summary>
    partial void OnCurrentPageChanged(PluginCenterPageType value)
    {
        // 刷新页面数据（通过 PageViewModel 调用）
        switch (value)
        {
        case PluginCenterPageType.MyProfiles:
            _myProfilesPageVM.RefreshProfileList();
            break;
        case PluginCenterPageType.ProfileMarket:
            // Fire-and-forget: 异步加载但不阻塞 UI
            _ = _profileMarketPageVM.LoadProfilesAsync();
            break;
        case PluginCenterPageType.InstalledPlugins:
            _ = _installedPluginsPageVM.CheckAndRefreshPluginListAsync();
            break;
        case PluginCenterPageType.AvailablePlugins:
            _availablePluginsPageVM.RefreshPluginList();
            break;
        }
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    public void RefreshCurrentPage()
    {
        switch (CurrentPage)
        {
        case PluginCenterPageType.MyProfiles:
            _myProfilesPageVM.RefreshProfileList();
            break;
        case PluginCenterPageType.ProfileMarket:
            // Fire-and-forget: 异步加载但不阻塞 UI
            _ = _profileMarketPageVM.LoadProfilesAsync();
            break;
        case PluginCenterPageType.InstalledPlugins:
            _ = _installedPluginsPageVM.CheckAndRefreshPluginListAsync();
            break;
        case PluginCenterPageType.AvailablePlugins:
            _availablePluginsPageVM.RefreshPluginList();
            break;
        }
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
