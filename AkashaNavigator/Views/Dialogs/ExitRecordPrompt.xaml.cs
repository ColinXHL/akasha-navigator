using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 退出记录提示窗口
    /// 当用户退出应用且当前页面未记录时显示
    /// </summary>
    public partial class ExitRecordPrompt : AnimatedWindow
    {
        private readonly ExitRecordPromptViewModel _viewModel;

        /// <summary>
        /// 用户选择的操作结果
        /// </summary>
        public PromptResult Result => _viewModel.Result;

        /// <summary>
        /// DI容器注入的构造函数
        /// </summary>
        public ExitRecordPrompt(ExitRecordPromptViewModel viewModel, IPioneerNoteService pioneerNoteService)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;

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
        private void OnRequestClose(object? sender, EventArgs e)
        {
            CloseWithAnimation();
        }

        /// <summary>
        /// 检查是否需要显示退出记录提示
        /// </summary>
        /// <param name="url">当前页面 URL</param>
        /// <param name="pioneerNoteService">开荒笔记服务</param>
        /// <returns>如果 URL 未记录且非空，返回 true</returns>
        public static bool ShouldShowPrompt(string url, IPioneerNoteService pioneerNoteService)
        {
            // 如果 URL 为空，不显示提示
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // 检查 URL 是否已记录
            return !pioneerNoteService.IsUrlRecorded(url);
        }
    }
}
