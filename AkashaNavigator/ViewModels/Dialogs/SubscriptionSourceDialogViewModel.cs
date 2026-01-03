using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 订阅源管理对话框 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class SubscriptionSourceDialogViewModel : ObservableObject
    {
        private readonly ProfileMarketplaceService _profileMarketplaceService;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// 订阅源列表
        /// </summary>
        public ObservableCollection<SubscriptionSourceItemViewModel> Sources { get; } = new();

        /// <summary>
        /// URL 输入文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        private string _urlInput = string.Empty;

        /// <summary>
        /// 是否显示占位符文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _showPlaceholder = true;

        /// <summary>
        /// 是否显示空列表提示（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool _showEmptyHint = true;

        /// <summary>
        /// 是否正在添加（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        private bool _isAdding;

        /// <summary>
        /// 添加按钮文本（自动生成属性和通知）
        /// </summary>
        [ObservableProperty]
        private string _addButtonText = "添加";

        /// <summary>
        /// 是否有更改（用于 DialogResult）
        /// </summary>
        public bool HasChanges { get; private set; }

        /// <summary>
        /// 请求关闭对话框事件
        /// </summary>
        public event EventHandler<bool>? RequestClose;

        public SubscriptionSourceDialogViewModel(
            ProfileMarketplaceService profileMarketplaceService,
            INotificationService notificationService)
        {
            _profileMarketplaceService = profileMarketplaceService
                ?? throw new ArgumentNullException(nameof(profileMarketplaceService));
            _notificationService = notificationService
                ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// 初始化（加载订阅源列表）
        /// </summary>
        public void Initialize()
        {
            LoadSources();
        }

        /// <summary>
        /// URL 输入文本变化时（自动生成的方法）
        /// </summary>
        partial void OnUrlInputChanged(string value)
        {
            ShowPlaceholder = string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 加载订阅源列表
        /// </summary>
        private void LoadSources()
        {
            var sources = _profileMarketplaceService.GetSubscriptionSources();
            var viewModels = sources.Select(s => new SubscriptionSourceItemViewModel(s)).ToList();

            Sources.Clear();
            foreach (var vm in viewModels)
            {
                Sources.Add(vm);
            }

            ShowEmptyHint = Sources.Count == 0;
        }

        /// <summary>
        /// 添加订阅源命令（自动生成 AddCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAdd))]
        private async Task AddAsync()
        {
            var url = UrlInput?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                _notificationService.Info("请输入订阅源 URL", "提示");
                return;
            }

            IsAdding = true;
            AddButtonText = "添加中...";

            try
            {
                var result = await _profileMarketplaceService.AddSubscriptionSourceAsync(url);

                if (result.IsSuccess)
                {
                    UrlInput = string.Empty;
                    LoadSources();
                    HasChanges = true;

                    var message = "订阅源添加成功！";
                    if (!string.IsNullOrEmpty(result.SourceName))
                    {
                        message += $"\n\n名称: {result.SourceName}";
                    }
                    if (result.ProfileCount > 0)
                    {
                        message += $"\n包含 {result.ProfileCount} 个 Profile";
                    }

                    _notificationService.Success(message, "添加成功");
                }
                else
                {
                    _notificationService.Error($"添加失败: {result.ErrorMessage}", "添加失败");
                }
            }
            finally
            {
                IsAdding = false;
                AddButtonText = "添加";
            }
        }

        /// <summary>
        /// 是否可以添加（URL 不为空且不在添加中）
        /// </summary>
        private bool CanAdd() => !string.IsNullOrWhiteSpace(UrlInput) && !IsAdding;

        /// <summary>
        /// 删除订阅源命令（自动生成 RemoveCommand）
        /// </summary>
        [RelayCommand]
        private async Task RemoveAsync(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            var confirmed = await _notificationService.ConfirmAsync(
                $"确定要删除此订阅源吗？\n\n{url}", "确认删除");

            if (confirmed)
            {
                _profileMarketplaceService.RemoveSubscriptionSource(url);
                LoadSources();
                HasChanges = true;
            }
        }

        /// <summary>
        /// 关闭命令（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(this, HasChanges);
        }
    }

    /// <summary>
    /// 订阅源项视图模型
    /// </summary>
    public partial class SubscriptionSourceItemViewModel : ObservableObject
    {
        private readonly MarketplaceSource _source;

        public SubscriptionSourceItemViewModel(MarketplaceSource source)
        {
            _source = source;
        }

        public string Url => _source.Url;
        public string Name => _source.Name;
        public bool Enabled => _source.Enabled;
        public DateTime? LastFetched => _source.LastFetched;

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "未命名订阅源" : Name;
        public bool HasLastFetched => LastFetched.HasValue;
        public string LastFetchedText =>
            LastFetched.HasValue ? $"上次更新: {LastFetched.Value:yyyy-MM-dd HH:mm}" : string.Empty;
    }
}
