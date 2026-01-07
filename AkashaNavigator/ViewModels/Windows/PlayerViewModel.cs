using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.ViewModels.Windows
{
    /// <summary>
    /// 播放器 ViewModel - 处理播放器状态和导航业务逻辑
    /// 使用 CommunityToolkit.Mvvm 源生成器
    ///
    /// 职责划分（混合架构）：
    /// - ViewModel: 业务逻辑、状态、命令、EventBus 订阅
    /// - Code-Behind: WebView2 交互、窗口行为、UI 逻辑
    /// </summary>
    public partial class PlayerViewModel : ObservableObject
    {
        private readonly IProfileManager _profileManager;
        private readonly IEventBus _eventBus;

        /// <summary>
        /// 当前 Profile（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private GameProfile? _currentProfile;

        /// <summary>
        /// 当前 URL（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _currentUrl = string.Empty;

        /// <summary>
        /// 是否正在加载（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// 导航请求事件（由 Code-behind 订阅，用于执行实际导航）
        /// </summary>
        public event EventHandler<string>? NavigationRequested;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PlayerViewModel(IProfileManager profileManager, IEventBus eventBus)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            // 加载当前 Profile
            CurrentProfile = _profileManager.CurrentProfile;

            // 订阅事件
            SubscribeToEvents();
        }

        /// <summary>
        /// 订阅 EventBus 事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventBus.Subscribe<NavigationRequestedEvent>(OnNavigationRequested);
            _eventBus.Subscribe<NavigationControlEvent>(OnNavigationControl);
            _eventBus.Subscribe<HistoryItemSelectedEvent>(OnHistoryItemSelected);
            _eventBus.Subscribe<BookmarkItemSelectedEvent>(OnBookmarkItemSelected);
            _eventBus.Subscribe<PioneerNoteItemSelectedEvent>(OnPioneerNoteItemSelected);
        }

        /// <summary>
        /// 处理导航请求事件
        /// </summary>
        private void OnNavigationRequested(NavigationRequestedEvent e)
        {
            if (!string.IsNullOrEmpty(e.Url))
            {
                Navigate(e.Url);
            }
        }

        /// <summary>
        /// 处理导航控制事件
        /// </summary>
        private void OnNavigationControl(NavigationControlEvent e)
        {
            switch (e.Action)
            {
                case NavigationControlAction.Back:
                    Back();
                    break;
                case NavigationControlAction.Forward:
                    Forward();
                    break;
                case NavigationControlAction.Refresh:
                    Refresh();
                    break;
            }
        }

        /// <summary>
        /// 处理历史记录项选中事件
        /// </summary>
        private void OnHistoryItemSelected(HistoryItemSelectedEvent e)
        {
            if (!string.IsNullOrEmpty(e.Url))
            {
                Navigate(e.Url);
            }
        }

        /// <summary>
        /// 处理收藏夹项选中事件
        /// </summary>
        private void OnBookmarkItemSelected(BookmarkItemSelectedEvent e)
        {
            if (!string.IsNullOrEmpty(e.Url))
            {
                Navigate(e.Url);
            }
        }

        /// <summary>
        /// 处理开荒笔记项选中事件
        /// </summary>
        private void OnPioneerNoteItemSelected(PioneerNoteItemSelectedEvent e)
        {
            if (!string.IsNullOrEmpty(e.Url))
            {
                Navigate(e.Url);
            }
        }

        /// <summary>
        /// 更新当前 URL（由 Code-behind 调用）
        /// </summary>
        public void UpdateCurrentUrl(string url)
        {
            CurrentUrl = url;
        }

        /// <summary>
        /// 导航到指定 URL
        /// </summary>
        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // URL 验证和标准化（业务逻辑）
            string targetUrl = url.Trim();

            // 自动补全 URL scheme
            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = "https://" + targetUrl;
            }

            CurrentUrl = targetUrl;
            IsLoading = true;

            // 实际导航由 Code-behind 的 WebView2 执行
            NavigationRequested?.Invoke(this, targetUrl);
        }

        /// <summary>
        /// 后退（自动生成 BackCommand）
        /// </summary>
        [RelayCommand]
        private void Back()
        {
            // 通过 EventBus 发布后退命令
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Back });
        }

        /// <summary>
        /// 前进（自动生成 ForwardCommand）
        /// </summary>
        [RelayCommand]
        private void Forward()
        {
            // 通过 EventBus 发布前进命令
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Forward });
        }

        /// <summary>
        /// 刷新（自动生成 RefreshCommand）
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            // 通过 EventBus 发布刷新命令
            _eventBus.Publish(new NavigationControlEvent { Action = NavigationControlAction.Refresh });
        }

        /// <summary>
        /// 导航完成处理（由 Code-behind 调用）
        /// </summary>
        public void OnNavigationCompleted(bool isSuccess)
        {
            IsLoading = false;
        }
    }
}
