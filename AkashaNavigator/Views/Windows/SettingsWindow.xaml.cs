using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Windows;
using AkashaNavigator.ViewModels.Pages.Settings;
using AkashaNavigator.Views.Pages;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// SettingsWindow - 设置窗口（MVVM 架构）
/// 快捷键逻辑已移至 HotkeyTextBox 自定义控件
/// </summary>
public partial class SettingsWindow : AnimatedWindow
{
    private readonly SettingsViewModel _viewModel;
    private readonly INotificationService _notificationService;
    private readonly GeneralSettingsPage _generalPage;
    private readonly WindowSettingsPage _windowPage;
    private readonly HotkeySettingsPage _hotkeysPage;
    private readonly AdvancedSettingsPage _advancedPage;

    public SettingsWindow(SettingsViewModel viewModel,
                          INotificationService notificationService,
                          GeneralSettingsPage generalPage,
                          WindowSettingsPage windowPage,
                          HotkeySettingsPage hotkeysPage,
                          AdvancedSettingsPage advancedPage)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _generalPage = generalPage ?? throw new ArgumentNullException(nameof(generalPage));
        _windowPage = windowPage ?? throw new ArgumentNullException(nameof(windowPage));
        _hotkeysPage = hotkeysPage ?? throw new ArgumentNullException(nameof(hotkeysPage));
        _advancedPage = advancedPage ?? throw new ArgumentNullException(nameof(advancedPage));

        InitializeComponent();
        DataContext = _viewModel;

        // 订阅 ViewModel 事件
        _viewModel.OpenConfigFolderRequested += OnOpenConfigFolder;

        // 加载页面
        LoadPages();
        UpdatePageVisibility(_viewModel.CurrentPage);

        // 订阅 ViewModel 的 PropertyChanged 事件，处理页面显示切换
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.CurrentPage))
            {
                UpdatePageVisibility(_viewModel.CurrentPage);
            }
        };

        // 订阅搜索结果变化
        _viewModel.SearchResults.CollectionChanged += SearchResults_CollectionChanged;

        // 加载 Profile 列表（通过 GeneralPage）
        _viewModel.RefreshProfileList();
    }

    /// <summary>
    /// 搜索结果集合变化时更新 Popup 显示
    /// </summary>
    private void SearchResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 在 UI 线程上执行
        Dispatcher.InvokeAsync(() =>
        {
            if (_viewModel.SearchResults.Count > 0 && SearchBox.IsFocused)
            {
                SearchResultsPopup.IsOpen = true;
                NoResultsPopup.IsOpen = false;
            }
            else if (_viewModel.SearchResults.Count == 0 && !string.IsNullOrWhiteSpace(_viewModel.SearchQuery))
            {
                SearchResultsPopup.IsOpen = false;
                NoResultsPopup.IsOpen = true;
            }
            else
            {
                SearchResultsPopup.IsOpen = false;
                NoResultsPopup.IsOpen = false;
            }
        });
    }

    /// <summary>
    /// 搜索框获得焦点时如果有文字则显示对应 Popup
    /// </summary>
    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.SearchQuery))
        {
            if (_viewModel.SearchResults.Count > 0)
            {
                SearchResultsPopup.IsOpen = true;
            }
            else
            {
                NoResultsPopup.IsOpen = true;
            }
        }
    }

    /// <summary>
    /// 加载所有 Pages
    /// </summary>
    private void LoadPages()
    {
        ContentArea.Children.Add(_generalPage);
        ContentArea.Children.Add(_windowPage);
        ContentArea.Children.Add(_hotkeysPage);
        ContentArea.Children.Add(_advancedPage);
    }

    /// <summary>
    /// UI 逻辑：页面显示切换
    /// </summary>
    private void UpdatePageVisibility(SettingsPageType currentPage)
    {
        _generalPage.Visibility = currentPage == SettingsPageType.General ? Visibility.Visible : Visibility.Collapsed;
        _windowPage.Visibility = currentPage == SettingsPageType.Window ? Visibility.Visible : Visibility.Collapsed;
        _hotkeysPage.Visibility = currentPage == SettingsPageType.Hotkeys ? Visibility.Visible : Visibility.Collapsed;
        _advancedPage.Visibility = currentPage == SettingsPageType.Advanced ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 打开配置文件夹
    /// </summary>
    private void OnOpenConfigFolder(object? sender, string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }
    }

    /// <summary>
    /// 取消按钮
    /// </summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    /// <summary>
    /// 保存按钮
    /// </summary>
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCommand.Execute(null);
        CloseWithAnimation();
    }
}
}
