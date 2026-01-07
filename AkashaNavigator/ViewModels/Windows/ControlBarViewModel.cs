using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;

namespace AkashaNavigator.ViewModels.Windows
{
    /// <summary>
    /// 控制栏 ViewModel - 处理业务逻辑，不处理 UI 状态
    /// 统一使用 EventBus 通信
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class ControlBarViewModel : ObservableObject
    {
        private readonly IEventBus _eventBus;

        /// <summary>
        /// 当前 URL（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _currentUrl = string.Empty;

        /// <summary>
        /// 是否可以后退（自动生成属性和通知，关联 BackCommand）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BackCommand))]
        private bool _canGoBack;

        /// <summary>
        /// 是否可以前进（自动生成属性和通知，关联 ForwardCommand）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ForwardCommand))]
        private bool _canGoForward;

        /// <summary>
        /// 当前页面是否已收藏（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _isBookmarked;

        /// <summary>
        /// 当前页面标题（用于收藏和记录笔记）
        /// </summary>
        [ObservableProperty]
        private string _currentTitle = string.Empty;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ControlBarViewModel(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            SubscribeToEvents();
        }

        /// <summary>
        /// 订阅 EventBus 事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventBus.Subscribe<UrlChangedEvent>(OnUrlChanged);
            _eventBus.Subscribe<NavigationStateChangedEvent>(OnNavigationStateChanged);
            _eventBus.Subscribe<BookmarkStateChangedEvent>(OnBookmarkStateChanged);
        }

        /// <summary>
        /// 处理 URL 变化事件
        /// </summary>
        private void OnUrlChanged(UrlChangedEvent e)
        {
            CurrentUrl = e.Url;
        }

        /// <summary>
        /// 处理导航状态变化事件
        /// </summary>
        private void OnNavigationStateChanged(NavigationStateChangedEvent e)
        {
            CanGoBack = e.CanGoBack;
            CanGoForward = e.CanGoForward;
        }

        /// <summary>
        /// 处理收藏状态变化事件
        /// </summary>
        private void OnBookmarkStateChanged(BookmarkStateChangedEvent e)
        {
            IsBookmarked = e.IsBookmarked;
        }

        /// <summary>
        /// 导航命令（自动生成 NavigateCommand）
        /// </summary>
        [RelayCommand]
        private void Navigate(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // URL 验证（业务逻辑）
            string targetUrl = url.Trim();

            // 自动补全 URL scheme
            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = "https://" + targetUrl;
            }

            _eventBus.Publish(new NavigationRequestedEvent { Url = targetUrl });
        }

        /// <summary>
        /// 后退命令（自动生成 BackCommand，CanExecute 由 CanGoBack 控制）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void Back()
        {
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Back });
        }

        /// <summary>
        /// 前进命令（自动生成 ForwardCommand，CanExecute 由 CanGoForward 控制）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private void Forward()
        {
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Forward });
        }

        /// <summary>
        /// 刷新命令（自动生成 RefreshCommand）
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Refresh });
        }

        /// <summary>
        /// 收藏命令（自动生成 BookmarkCommand）
        /// BookmarkRequestedEvent 本身就是 Toggle 操作
        /// </summary>
        [RelayCommand]
        private void ToggleBookmark()
        {
            _eventBus.Publish(new BookmarkRequestedEvent
            {
                Url = CurrentUrl,
                Title = CurrentTitle
            });
        }

        /// <summary>
        /// 记录笔记命令（自动生成 RecordNoteCommand）
        /// </summary>
        [RelayCommand]
        private void RecordNote()
        {
            _eventBus.Publish(new RecordNoteRequestedEvent
            {
                Url = CurrentUrl,
                Title = CurrentTitle
            });
        }

        /// <summary>
        /// 显示历史命令（自动生成 ShowHistoryCommand）
        /// </summary>
        [RelayCommand]
        private void ShowHistory()
        {
            _eventBus.Publish(new HistoryRequestedEvent());
        }

        /// <summary>
        /// 显示收藏夹命令（自动生成 ShowBookmarksCommand）
        /// </summary>
        [RelayCommand]
        private void ShowBookmarks()
        {
            _eventBus.Publish(new BookmarksRequestedEvent());
        }

        /// <summary>
        /// 显示开荒笔记命令（自动生成 ShowPioneerNotesCommand）
        /// </summary>
        [RelayCommand]
        private void ShowPioneerNotes()
        {
            _eventBus.Publish(new PioneerNotesRequestedEvent());
        }

        /// <summary>
        /// 显示设置命令（自动生成 ShowSettingsCommand）
        /// </summary>
        [RelayCommand]
        private void ShowSettings()
        {
            _eventBus.Publish(new SettingsRequestedEvent());
        }

        /// <summary>
        /// 显示插件中心命令（自动生成 ShowPluginCenterCommand）
        /// </summary>
        [RelayCommand]
        private void ShowPluginCenter()
        {
            _eventBus.Publish(new PluginCenterRequestedEvent());
        }

        /// <summary>
        /// 显示菜单命令（自动生成 ShowMenuCommand）
        /// </summary>
        [RelayCommand]
        private void ShowMenu()
        {
            _eventBus.Publish(new MenuRequestedEvent { MenuType = MenuType.History });
        }
    }
}
