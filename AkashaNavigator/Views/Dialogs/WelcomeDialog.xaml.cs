using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 首次启动欢迎弹窗
    /// </summary>
    public partial class WelcomeDialog : AnimatedWindow
    {
        private readonly WelcomeDialogViewModel _viewModel;

        public WelcomeDialog(WelcomeDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // 订阅关闭请求事件
            _viewModel.CloseRequested += OnCloseRequested;
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 标题栏拖动（UI 逻辑保留）
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }
    }
}
