using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 订阅源管理对话框
    /// </summary>
    public partial class SubscriptionSourceDialog : Window
    {
        private readonly SubscriptionSourceDialogViewModel _viewModel;

        /// <summary>
        /// DI容器注入的构造函数
        /// </summary>
        public SubscriptionSourceDialog(SubscriptionSourceDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            Loaded += SubscriptionSourceDialog_Loaded;
            _viewModel.RequestClose += OnRequestClose;
        }

        private void SubscriptionSourceDialog_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Initialize();
            UrlInput.Focus();
        }

        /// <summary>
        /// URL 输入框按键事件（UI 逻辑：处理 Enter 键）
        /// </summary>
        private void UrlInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.AddCommand.CanExecute(null))
                {
                    _viewModel.AddCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, bool hasChanges)
        {
            DialogResult = hasChanges;
            Close();
        }
    }
}
