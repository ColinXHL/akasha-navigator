using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 笔记移动对话框
    /// 用于选择目标目录移动笔记项
    /// </summary>
    public partial class NoteMoveDialog : AnimatedWindow
    {
        #region Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消
        /// </summary>
        public bool Result { get; private set; }

        /// <summary>
        /// 选中的目录 ID（null 表示根目录）
        /// </summary>
        public string? SelectedFolderId => _viewModel?.SelectedFolderId;

        #endregion

        #region Constructor

        private readonly NoteMoveDialogViewModel _viewModel;

        /// <summary>
        /// 创建移动对话框
        /// </summary>
        /// <param name="viewModel">ViewModel</param>
        public NoteMoveDialog(NoteMoveDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // 订阅 ViewModel 的关闭请求事件
            _viewModel.RequestClose += OnRequestClose;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, bool? result)
        {
            if (result.HasValue)
            {
                Result = result.Value;
            }
            CloseWithAnimation();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseWithAnimation();
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        #endregion
    }
}
