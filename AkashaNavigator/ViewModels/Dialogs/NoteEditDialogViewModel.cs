using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 笔记编辑对话框的 ViewModel
    /// 用于编辑笔记项或目录的名称，支持多种模式（编辑、确认对话框等）
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class NoteEditDialogViewModel : ObservableObject
    {
        #region Fields

        private readonly bool _showUrl;
        private readonly bool _isConfirmDialog;

        #endregion

        #region Properties

        /// <summary>
        /// 对话框标题
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 提示文本
        /// </summary>
        public string Prompt { get; }

        /// <summary>
        /// 是否显示 URL 输入框
        /// </summary>
        public bool ShowUrl => _showUrl;

        /// <summary>
        /// 是否为确认对话框模式（只显示消息，不显示输入框）
        /// </summary>
        public bool IsConfirmDialog => _isConfirmDialog;

        #endregion

        #region Observable Properties

        /// <summary>
        /// 对话框结果：true=确定，false=取消（自动生成 DialogResult 属性和通知）
        /// </summary>
        [ObservableProperty]
        private bool? _dialogResult;

        /// <summary>
        /// 输入的文本（自动生成 InputText 属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _inputText = string.Empty;

        /// <summary>
        /// URL 文本（自动生成 UrlText 属性和通知）
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _urlText = string.Empty;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建笔记编辑对话框 ViewModel
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="prompt">提示文本</param>
        /// <param name="showUrl">是否显示 URL 输入框</param>
        /// <param name="isConfirmDialog">是否为确认对话框（只显示消息和按钮）</param>
        /// <param name="defaultUrl">默认 URL 值（仅在 showUrl 为 true 时有效）</param>
        public NoteEditDialogViewModel(
            string title,
            string defaultValue,
            string prompt = "请输入新名称：",
            bool showUrl = false,
            bool isConfirmDialog = false,
            string? defaultUrl = null)
        {
            Title = title ?? string.Empty;
            Prompt = prompt ?? "请输入新名称：";
            _showUrl = showUrl;
            _isConfirmDialog = isConfirmDialog;

            InputText = defaultValue ?? string.Empty;

            if (showUrl && !string.IsNullOrWhiteSpace(defaultUrl))
            {
                UrlText = defaultUrl;
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// 确定命令（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            DialogResult = true;
        }

        /// <summary>
        /// 是否可以确认（输入验证）
        /// </summary>
        private bool CanConfirm()
        {
            // 确认对话框模式下始终可以确认
            if (_isConfirmDialog)
            {
                return true;
            }

            var titleValid = !string.IsNullOrWhiteSpace(InputText);
            var urlValid = !_showUrl || !string.IsNullOrWhiteSpace(UrlText);
            return titleValid && urlValid;
        }

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
        }

        /// <summary>
        /// 关闭命令（自动生成 CloseCommand）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            DialogResult = false;
        }

        /// <summary>
        /// 获取当前 URL 命令（自动生成 GetCurrentUrlCommand）
        /// 由 Code-behind 调用，通过 Owner 链查找 PlayerWindow
        /// </summary>
        [RelayCommand]
        private void GetCurrentUrl(string? currentUrl)
        {
            if (!string.IsNullOrWhiteSpace(currentUrl))
            {
                UrlText = currentUrl;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 获取输入的文本（去除首尾空格）
        /// </summary>
        public string GetTrimmedInputText() => InputText?.Trim() ?? string.Empty;

        /// <summary>
        /// 获取 URL 文本（去除首尾空格）
        /// </summary>
        public string GetTrimmedUrlText() => UrlText?.Trim() ?? string.Empty;

        #endregion
    }
}
