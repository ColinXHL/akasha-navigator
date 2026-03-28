using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 市场 Profile 详情对话框
    /// </summary>
    public partial class MarketplaceProfileDetailDialog : AnimatedWindow
    {
        private readonly MarketplaceProfileDetailDialogViewModel _viewModel;

        /// <summary>
        /// 是否应该安装
        /// </summary>
        public bool ShouldInstall { get; private set; }

        public MarketplaceProfileDetailAction Action => _viewModel.Action;

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
            _viewModel.RequestClose += OnRequestClose;
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            ShouldInstall = dialogResult == true;
            DialogResult = dialogResult;
            Close();
        }
    }
}
