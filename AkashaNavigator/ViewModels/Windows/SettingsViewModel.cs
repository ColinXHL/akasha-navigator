using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Settings;
using AkashaNavigator.ViewModels.Pages.Settings;

namespace AkashaNavigator.ViewModels.Windows;

/// <summary>
/// 设置窗口 ViewModel - 容器模式
/// 遵循 MVVM 原则：ViewModel 只依赖 PageViewModel，不直接引用 View
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly GeneralSettingsPageViewModel _generalPageVM;
    private readonly WindowSettingsPageViewModel _windowPageVM;
    private readonly HotkeySettingsPageViewModel _hotkeysPageVM;
    private readonly AdvancedSettingsPageViewModel _advancedPageVM;
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
    private readonly IEventBus _eventBus;

    /// <summary>
    /// 当前显示的页面类型（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private SettingsPageType _currentPage = SettingsPageType.General;

    /// <summary>
    /// 搜索查询文本（自动生成属性和通知，防抖延迟 300ms 在 XAML 中配置）
    /// </summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>
    /// 搜索结果集合
    /// </summary>
    public ObservableCollection<SearchResult> SearchResults { get; } = new();

    /// <summary>
    /// 是否有搜索结果（用于 UI 显示）
    /// </summary>
    public bool HasSearchResults => SearchResults.Count > 0;

    /// <summary>
    /// 是否无搜索结果（用于 UI 显示提示）
    /// </summary>
    public bool HasNoSearchResults => !string.IsNullOrWhiteSpace(SearchQuery) && SearchResults.Count == 0;

    /// <summary>
    /// 是否正在搜索（用于 UI 显示分隔线）
    /// </summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);

    /// <summary>
    /// 通用设置页面 ViewModel
    /// </summary>
    public GeneralSettingsPageViewModel GeneralPage => _generalPageVM;

    /// <summary>
    /// 窗口设置页面 ViewModel
    /// </summary>
    public WindowSettingsPageViewModel WindowPage => _windowPageVM;

    /// <summary>
    /// 快捷键设置页面 ViewModel
    /// </summary>
    public HotkeySettingsPageViewModel HotkeysPage => _hotkeysPageVM;

    /// <summary>
    /// 高级设置页面 ViewModel
    /// </summary>
    public AdvancedSettingsPageViewModel AdvancedPage => _advancedPageVM;

    public SettingsViewModel(IConfigService configService, IProfileManager profileManager, IEventBus eventBus,
                             GeneralSettingsPageViewModel generalPageVM, WindowSettingsPageViewModel windowPageVM,
                             HotkeySettingsPageViewModel hotkeysPageVM, AdvancedSettingsPageViewModel advancedPageVM)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _generalPageVM = generalPageVM ?? throw new ArgumentNullException(nameof(generalPageVM));
        _windowPageVM = windowPageVM ?? throw new ArgumentNullException(nameof(windowPageVM));
        _hotkeysPageVM = hotkeysPageVM ?? throw new ArgumentNullException(nameof(hotkeysPageVM));
        _advancedPageVM = advancedPageVM ?? throw new ArgumentNullException(nameof(advancedPageVM));

        var config = _configService.Config;

        // 从各 PageViewModel 加载设置
        _windowPageVM.LoadSettings(config);
        _hotkeysPageVM.LoadHotkeys(config);
        _advancedPageVM.LoadSettings(config);

        // 订阅快捷键变化事件，自动检测冲突
        foreach (var binding in _hotkeysPageVM.HotkeyBindings)
        {
            binding.BindingChanged += (s, e) => _hotkeysPageVM.UpdateConflictStatus();
        }

        // 初始化搜索索引
        InitializeSearchIndex();
    }

    /// <summary>
    /// 可搜索的设置项索引
    /// </summary>
    private readonly List<SearchableSetting> _searchableSettings = new();

    /// <summary>
    /// 初始化搜索索引（定义所有可搜索的设置项）
    /// </summary>
    private void InitializeSearchIndex()
    {
        _searchableSettings.Clear();

        // ===== 通用设置 =====
        _searchableSettings.AddRange(new[] {
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "快进秒数", "基础设置"),
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "倒退秒数", "基础设置"),
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "默认透明度", "基础设置"),
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "Profile", "配置"),
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "配置文件夹", "配置"),
            new SearchableSetting(SettingsPageType.General, "⚙️ 通用", "插件中心", "配置"),
        });

        // ===== 窗口设置 =====
        _searchableSettings.AddRange(new[] {
            new SearchableSetting(SettingsPageType.Window, "🔲 窗口", "边缘吸附", "窗口行为"),
            new SearchableSetting(SettingsPageType.Window, "🔲 窗口", "吸附阈值", "窗口行为"),
            new SearchableSetting(SettingsPageType.Window, "🔲 窗口", "退出提示", "窗口行为"),
            new SearchableSetting(SettingsPageType.Window, "🔲 窗口", "记录笔记", "窗口行为"),
        });

        // ===== 快捷键设置 =====
        _searchableSettings.AddRange(new[] {
            // 全局控制
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "禁用快捷键", "全局控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "启用快捷键", "全局控制"),
            // 视频控制
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "播放暂停", "视频控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "快进", "视频控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "倒退", "视频控制"),
            // 透明度控制
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "透明度", "透明度控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "增加透明度", "透明度控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "降低透明度", "透明度控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "重置透明度", "透明度控制"),
            // 窗口行为
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "鼠标穿透", "窗口行为"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "点击穿透", "窗口行为"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "最大化", "窗口行为"),
            // 播放速率
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "播放速率", "播放速率"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "增加速率", "播放速率"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "减少速率", "播放速率"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "重置速率", "播放速率"),
            // 窗口控制
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "显示窗口", "窗口控制"),
            new SearchableSetting(SettingsPageType.Hotkeys, "⌨️ 快捷键", "隐藏窗口", "窗口控制"),
        });

        // ===== 高级设置 =====
        _searchableSettings.AddRange(new[] {
            new SearchableSetting(SettingsPageType.Advanced, "🔧 高级", "插件更新", "高级设置"),
            new SearchableSetting(SettingsPageType.Advanced, "🔧 高级", "更新提示", "高级设置"),
            new SearchableSetting(SettingsPageType.Advanced, "🔧 高级", "调试日志", "高级设置"),
            new SearchableSetting(SettingsPageType.Advanced, "🔧 高级", "日志", "高级设置"),
        });
    }

    /// <summary>
    /// SearchQuery 属性变化时触发搜索
    /// </summary>
    partial void OnSearchQueryChanged(string value)
    {
        PerformSearch(value);
        // 通知相关属性变化
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(IsSearching));
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    private void PerformSearch(string query)
    {
        SearchResults.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var searchTerm = query.Trim().ToLowerInvariant();
        var results = new List<SearchResult>();

        // 1. 搜索页面名称
        var pageNames = new Dictionary<SettingsPageType, string> { { SettingsPageType.General, "⚙️ 通用" },
                                                                   { SettingsPageType.Window, "🔲 窗口" },
                                                                   { SettingsPageType.Hotkeys, "⌨️ 快捷键" },
                                                                   { SettingsPageType.Advanced, "🔧 高级" } };

        // 匹配页面名称（去除图标后匹配）
        foreach (var page in pageNames)
        {
            var plainName = page.Value.Replace("⚙️ ", "").Replace("🔲 ", "").Replace("⌨️ ", "").Replace("🔧 ", "");
            if (plainName.Contains(searchTerm))
            {
                results.Add(SearchResult.CreatePageResult(page.Key, page.Value));
            }
        }

        // 2. 搜索设置项
        foreach (var setting in _searchableSettings)
        {
            // 匹配设置项名称
            if (setting.SettingName.ToLowerInvariant().Contains(searchTerm))
            {
                results.Add(SearchResult.CreateSettingResult(setting.PageType, setting.PageDisplayName,
                                                             setting.SettingName, setting.GroupName));
            }
            // 匹配分组名称
            else if (!string.IsNullOrEmpty(setting.GroupName) &&
                     setting.GroupName.ToLowerInvariant().Contains(searchTerm))
            {
                results.Add(SearchResult.CreateSettingResult(setting.PageType, setting.PageDisplayName,
                                                             setting.SettingName, setting.GroupName));
            }
        }

        // 去重并排序（页面优先，然后按名称排序）
        var uniqueResults = results.GroupBy(r => new { r.PageType, r.SettingDisplayName })
                                .Select(g => g.First())
                                .OrderBy(r => r.ResultType == SearchResultType.Page ? 0 : 1)
                                .ThenBy(r => r.PageType)
                                .ThenBy(r => r.SettingDisplayName)
                                .ToList();

        foreach (var result in uniqueResults)
        {
            SearchResults.Add(result);
        }
    }

    /// <summary>
    /// 导航到通用设置页面（自动生成 NavigateToGeneralCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToGeneral()
    {
        CurrentPage = SettingsPageType.General;
    }

    /// <summary>
    /// 导航到窗口设置页面（自动生成 NavigateToWindowCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToWindow()
    {
        CurrentPage = SettingsPageType.Window;
    }

    /// <summary>
    /// 导航到快捷键设置页面（自动生成 NavigateToHotkeysCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToHotkeys()
    {
        CurrentPage = SettingsPageType.Hotkeys;
    }

    /// <summary>
    /// 导航到高级设置页面（自动生成 NavigateToAdvancedCommand）
    /// </summary>
    [RelayCommand]
    private void NavigateToAdvanced()
    {
        CurrentPage = SettingsPageType.Advanced;
    }

    /// <summary>
    /// 导航到搜索结果（自动生成 NavigateToSearchResultCommand）
    /// 点击搜索结果后跳转到对应页面
    /// </summary>
    [RelayCommand]
    private void NavigateToSearchResult(SearchResult? result)
    {
        if (result == null)
            return;

        // 跳转到目标页面
        CurrentPage = result.NavigationTarget;

        // 清空搜索框（可选，提升用户体验）
        SearchQuery = string.Empty;
    }

    /// <summary>
    /// 保存设置（自动生成 SaveCommand）
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        // 每次保存时获取最新的 Config 对象，避免使用过时的引用
        var config = _configService.Config;

        // 从各 PageViewModel 收集数据
        _generalPageVM.SaveSettings(config);
        _windowPageVM.SaveSettings(config);
        _hotkeysPageVM.SaveSettings(config);
        _advancedPageVM.SaveSettings(config);

        _configService.UpdateConfig(config);
    }

    /// <summary>
    /// 重置为默认设置（自动生成 ResetCommand）
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        var defaultConfig = new AppConfig();
        LoadFromConfig(defaultConfig);
    }

    /// <summary>
    /// 从配置对象重置设置
    /// </summary>
    private void LoadFromConfig(AppConfig config)
    {
        // 重置各 PageViewModel
        _generalPageVM.ResetSettings(config);
        _windowPageVM.ResetSettings(config);
        _hotkeysPageVM.ResetSettings(config);
        _advancedPageVM.ResetSettings(config);
    }

    /// <summary>
    /// 打开插件中心（自动生成 OpenPluginCenterCommand）
    /// </summary>
    [RelayCommand]
    private void OpenPluginCenter()
    {
        // 通过 EventBus 发布请求（解耦 PluginCenterWindow）
        _eventBus.Publish(new PluginCenterRequestedEvent());
    }

    /// <summary>
    /// 打开配置文件夹（自动生成 OpenConfigFolderCommand）
    /// </summary>
    [RelayCommand]
    private void OpenConfigFolder()
    {
        // 通过事件通知 Code-behind，传递路径参数
        var path = _profileManager.DataDirectory;
        OpenConfigFolderRequested?.Invoke(this, path);
    }

    /// <summary>
    /// 请求打开配置文件夹事件（参数：配置目录路径）
    /// </summary>
    public event EventHandler<string>? OpenConfigFolderRequested;

    /// <summary>
    /// 插件中心关闭后请求刷新 Profile 列表
    /// </summary>
    public void RefreshProfileList()
    {
        _generalPageVM.RefreshProfileList();
    }

    /// <summary>
    /// 刷新设置（设置窗口打开时调用）
    /// </summary>
    public void RefreshSettings()
    {
        _generalPageVM.RefreshSettings();
    }
}

/// <summary>
/// 可搜索的设置项记录
/// </summary>
/// <param name="PageType">页面类型</param>
/// <param name="PageDisplayName">页面显示名称</param>
/// <param name="SettingName">设置项名称</param>
/// <param name="GroupName">分组名称</param>
internal record SearchableSetting(SettingsPageType PageType, string PageDisplayName, string SettingName,
                                  string? GroupName);
