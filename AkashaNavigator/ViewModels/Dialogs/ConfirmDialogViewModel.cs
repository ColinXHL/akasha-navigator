using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AkashaNavigator.ViewModels.Dialogs
{
    /// <summary>
    /// 确认对话框 ViewModel
    /// 使用 CommunityToolkit.Mvvm 源生成器
    /// </summary>
    public partial class ConfirmDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 消息内容
        /// </summary>
        [ObservableProperty]
        private string _message;

        /// <summary>
        /// 标题
        /// </summary>
        [ObservableProperty]
        private string _title;

        /// <summary>
        /// 确定按钮文本
        /// </summary>
        [ObservableProperty]
        private string _confirmText;

        /// <summary>
        /// 取消按钮文本
        /// </summary>
        [ObservableProperty]
        private string _cancelText;

        /// <summary>
        /// 对话框结果：true=确定，false=取消，null=关闭按钮
        /// </summary>
        public bool? Result { get; private set; } = null;

        /// <summary>
        /// 对话框请求关闭事件
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="confirmText">确定按钮文本</param>
        /// <param name="cancelText">取消按钮文本</param>
        public ConfirmDialogViewModel(string message, string? title = null, string confirmText = "确定", string cancelText = "取消")
        {
            _message = message ?? string.Empty;
            _title = title ?? "确认";
            _confirmText = confirmText;
            _cancelText = cancelText;
        }

        /// <summary>
        /// 确定命令（自动生成 ConfirmCommand）
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            Result = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取消命令（自动生成 CancelCommand）
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Result = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 关闭命令（返回 false）
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            Result = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
