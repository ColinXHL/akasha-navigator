using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 市场 Profile 详情对话框
    /// </summary>
    public partial class MarketplaceProfileDetailDialog : Window
    {
        private readonly MarketplaceProfileDetailDialogViewModel _viewModel;

        /// <summary>
        /// 是否应该安装
        /// </summary>
        public bool ShouldInstall { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MarketplaceProfileDetailDialog(MarketplaceProfileDetailDialogViewModel viewModel, MarketplaceProfile profile)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;
            _viewModel.Initialize(profile);

            // 订阅关闭请求事件
            _viewModel.CloseRequested += OnCloseRequested;
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            ShouldInstall = _viewModel.DialogResult == true;
            DialogResult = _viewModel.DialogResult;
            Close();
        }
    }
}
