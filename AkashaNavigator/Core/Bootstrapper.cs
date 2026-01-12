using Microsoft.Extensions.DependencyInjection;
using System;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.ViewModels.Windows;

namespace AkashaNavigator.Core
{
/// <summary>
/// 应用程序启动引导器
/// 负责初始化 DI 容器和应用程序核心组件
/// </summary>
public class Bootstrapper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private PlayerWindow? _playerWindow;
    private ControlBarWindow? _controlBarWindow;

    public Bootstrapper()
    {
        // 配置服务并构建 DI 容器
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        _serviceProvider = services.BuildServiceProvider();
        _eventBus = _serviceProvider.GetRequiredService<IEventBus>();
    }

    /// <summary>
    /// 启动应用程序
    /// </summary>
    public void Run()
    {
        var sp = _serviceProvider;

        // 从 DI 容器获取主窗口
        _playerWindow = sp.GetRequiredService<PlayerWindow>();

        // 设置 PluginApi 的全局窗口获取器（在创建 PlayerWindow 后立即设置）
        PluginApi.SetGlobalWindowGetter(() => _playerWindow);

        // 加载当前 Profile 的插件
        var profileManager = sp.GetRequiredService<IProfileManager>();
        var pluginHost = sp.GetRequiredService<IPluginHost>();
        var currentProfileId = profileManager.CurrentProfile?.Id ?? "";
        pluginHost.LoadPluginsForProfile(currentProfileId);

        // 从 DI 容器获取控制栏窗口
        _controlBarWindow = sp.GetRequiredService<ControlBarWindow>();

        // 设置窗口关闭事件和菜单事件处理
        SetupEventHandlers();

        // 显示主窗口
        _playerWindow.Show();

        // 启动控制栏自动显示/隐藏
        _controlBarWindow.StartAutoShowHide();
    }

    /// <summary>
    /// 设置事件处理器
    /// 导航相关事件已由 EventBus 处理，这里只处理窗口关闭和菜单事件
    /// </summary>
    private void SetupEventHandlers()
    {
        if (_playerWindow == null || _controlBarWindow == null)
            return;

        // 播放器窗口关闭时，关闭控制栏
        _playerWindow.Closed += (s, e) =>
        { _controlBarWindow.Close(); };

        // 订阅 URL 变化事件，同步标题到 ControlBarWindow
        _eventBus.Subscribe<UrlChangedEvent>(e =>
                                             {
                                                 if (_playerWindow != null && _controlBarWindow != null)
                                                 {
                                                     var title = _playerWindow.CurrentTitle;
                                                     _controlBarWindow.UpdateCurrentTitle(title);
                                                 }
                                             });

        // 订阅菜单相关 EventBus 事件
        SetupMenuEventHandlers();
        SetupBookmarkEventHandlers();
    }

    /// <summary>
    /// 设置菜单事件处理器
    /// </summary>
    private void SetupMenuEventHandlers()
    {
        if (_playerWindow == null)
            return;

        var dataService = _serviceProvider.GetRequiredService<IDataService>();

        // 历史记录菜单事件
        _eventBus.Subscribe<HistoryRequestedEvent>(
            e =>
            {
                var historyWindow = _serviceProvider.GetRequiredService<HistoryWindow>();
                historyWindow.HistoryItemSelected += (sender, url) =>
                { _eventBus.Publish(new HistoryItemSelectedEvent { Url = url }); };
                historyWindow.ShowDialog();
            });

        // 收藏夹菜单事件
        _eventBus.Subscribe<BookmarksRequestedEvent>(
            e =>
            {
                var dialogFactory = _serviceProvider.GetRequiredService<IDialogFactory>();
                var bookmarkPopup = dialogFactory.CreateBookmarkPopup();
                bookmarkPopup.BookmarkItemSelected += (sender, url) =>
                { _eventBus.Publish(new BookmarkItemSelectedEvent { Url = url }); };
                bookmarkPopup.ShowDialog();
            });

        // 插件中心菜单事件
        _eventBus.Subscribe<PluginCenterRequestedEvent>(
            e =>
            {
                var pluginCenterWindow = _serviceProvider.GetRequiredService<PluginCenterWindow>();
                pluginCenterWindow.Owner = _playerWindow;
                pluginCenterWindow.ShowDialog();
            });

        // 设置菜单事件
        _eventBus.Subscribe<SettingsRequestedEvent>(e =>
                                                    {
                                                        var settingsWindow =
                                                            _serviceProvider.GetRequiredService<SettingsWindow>();
                                                        settingsWindow.Owner = _playerWindow;
                                                        settingsWindow.ShowDialog();
                                                    });

        // 记录笔记按钮点击事件
        _eventBus.Subscribe<RecordNoteRequestedEvent>(
            e =>
            {
                var recordDialogFactory = _serviceProvider.GetRequiredService<Func<string, string, RecordNoteDialog>>();
                var recordDialog = recordDialogFactory(e.Url, e.Title);
                recordDialog.Owner = _playerWindow;
                recordDialog.ShowDialog();
            });

        // 开荒笔记菜单事件
        _eventBus.Subscribe<PioneerNotesRequestedEvent>(
            e =>
            {
                var noteWindow = _serviceProvider.GetRequiredService<PioneerNoteWindow>();
                noteWindow.NoteItemSelected += (sender, url) =>
                { _eventBus.Publish(new PioneerNoteItemSelectedEvent { Url = url }); };
                noteWindow.Owner = _playerWindow;
                noteWindow.ShowDialog();
            });

        // 订阅 URL 变化事件，用于更新收藏状态
        _eventBus.Subscribe<UrlChangedEvent>(e =>
                                             {
                                                 if (!string.IsNullOrEmpty(e.Url))
                                                 {
                                                     var isBookmarked = dataService.IsBookmarked(e.Url);
                                                     _eventBus.Publish(
                                                         new BookmarkStateChangedEvent { IsBookmarked = isBookmarked });
                                                 }
                                             });
    }

    /// <summary>
    /// 设置收藏事件处理器
    /// </summary>
    private void SetupBookmarkEventHandlers()
    {
        var dataService = _serviceProvider.GetRequiredService<IDataService>();

        // 订阅收藏请求事件
        _eventBus.Subscribe<BookmarkRequestedEvent>(
            e =>
            {
                var isBookmarked = dataService.ToggleBookmark(e.Url, e.Title);
                _eventBus.Publish(new BookmarkStateChangedEvent { IsBookmarked = isBookmarked });
            });
    }

    /// <summary>
    /// 获取服务提供者（用于需要手动解析服务的场景）
    /// </summary>
    public IServiceProvider GetServiceProvider()
    {
        return _serviceProvider;
    }
}
}
