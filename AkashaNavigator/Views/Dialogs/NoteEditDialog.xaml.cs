using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 笔记编辑对话框
    /// 用于编辑笔记项或目录的名称
    /// </summary>
    public partial class NoteEditDialog : AnimatedWindow
    {
        #region Fields

        private readonly NoteEditDialogViewModel _viewModel;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建笔记编辑对话框
        /// </summary>
        /// <param name="viewModel">ViewModel</param>
        public NoteEditDialog(NoteEditDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;

            // 订阅 ViewModel 属性变化事件
            _viewModel.PropertyChanged += OnPropertyChanged;

            // 初始化 UI 状态
            InitializeUiState();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 初始化 UI 状态
        /// </summary>
        private void InitializeUiState()
        {
            // 如果是确认对话框模式，隐藏输入框
            if (_viewModel.IsConfirmDialog)
            {
                TxtInput.Visibility = Visibility.Collapsed;
                PromptText.TextWrapping = TextWrapping.Wrap;
                PromptText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                PromptText.FontSize = 13;
                PromptText.Margin = new Thickness(0, 0, 0, 16);
            }
            else
            {
                // 选中所有文本并聚焦输入框
                Loaded += (s, e) =>
                {
                    TxtInput.SelectAll();
                    TxtInput.Focus();
                };
            }
        }

        /// <summary>
        /// ViewModel 属性变化处理
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NoteEditDialogViewModel.DialogResult))
            {
                // 当 DialogResult 被设置时关闭窗口
                if (_viewModel.DialogResult.HasValue)
                {
                    CloseWithAnimation();
                }
            }
        }

        /// <summary>
        /// 获取当前 URL（通过 Owner 链查找 PlayerWindow）
        /// </summary>
        private string? GetCurrentUrlFromPlayerWindow()
        {
            var owner = Owner;
            while (owner != null)
            {
                if (owner is PlayerWindow playerWindow)
                {
                    return playerWindow.CurrentUrl;
                }
                owner = owner.Owner;
            }
            return null;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 主容器点击事件 - 取消输入框焦点
        /// </summary>
        private void MainContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FocusManager.SetFocusedElement(this, this);
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// 输入框按键事件（处理 Enter/Esc 快捷键）
        /// </summary>
        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.ConfirmCommand.CanExecute(null))
            {
                _viewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _viewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 获取当前 URL 按钮点击
        /// </summary>
        private void BtnGetCurrentUrl_Click(object sender, RoutedEventArgs e)
        {
            var currentUrl = GetCurrentUrlFromPlayerWindow();
            _viewModel.GetCurrentUrlCommand.Execute(currentUrl);
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消
        /// </summary>
        public bool? Result => _viewModel.DialogResult;

        /// <summary>
        /// 输入的文本
        /// </summary>
        public string InputText => _viewModel.GetTrimmedInputText();

        /// <summary>
        /// URL 文本（仅在 showUrl 模式下有效）
        /// </summary>
        public string UrlText => _viewModel.GetTrimmedUrlText();

        #endregion
    }
}
