using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 插件更新提示对话框
    /// </summary>
    public partial class PluginUpdatePromptDialog : AnimatedWindow
    {
        #region Fields

        private readonly PluginUpdatePromptDialogViewModel _viewModel;

        #endregion

        #region Properties

        /// <summary>
        /// 用户选择的操作结果
        /// </summary>
        public PluginUpdatePromptResult Result => _viewModel.Result;

        /// <summary>
        /// 用户是否选择了不再提示
        /// </summary>
        public bool DontShowAgain => _viewModel.DontShowAgain;

        #endregion

        #region Constructor

        /// <summary>
        /// 构造函数（接收 ViewModel）
        /// </summary>
        public PluginUpdatePromptDialog(PluginUpdatePromptDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // 订阅 ViewModel 的关闭请求事件
            _viewModel.RequestClose += OnRequestClose;
        }

        #endregion

        #region Event Handlers (UI 逻辑)

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, PluginUpdatePromptResult result)
        {
            // 设置 DialogResult 并关闭窗口
            DialogResult = result != PluginUpdatePromptResult.Cancel;
            Close();
        }

        #endregion
    }
}
