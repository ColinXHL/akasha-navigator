using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 插件卸载选择对话框 - 在卸载 Profile 时显示唯一插件列表供用户选择
    /// </summary>
    public partial class PluginUninstallDialog : AnimatedWindow
    {
        private readonly PluginUninstallDialogViewModel _viewModel;

        /// <summary>
        /// 用户是否确认卸载
        /// </summary>
        public bool Confirmed => _viewModel.Confirmed;

        /// <summary>
        /// 获取用户选择要卸载的插件 ID 列表
        /// </summary>
        public List<string> SelectedPluginIds => _viewModel.SelectedPluginIds;

        /// <summary>
        /// Profile 名称显示文本（用于 XAML 绑定）
        /// </summary>
        public string ProfileNameText => $"确定要卸载 \"{_viewModel.ProfileName}\" 吗？";

        /// <summary>
        /// 创建插件卸载选择对话框
        /// </summary>
        /// <param name="viewModel">ViewModel</param>
        public PluginUninstallDialog(PluginUninstallDialogViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            InitializeComponent();

            DataContext = _viewModel;
            _viewModel.RequestClose += OnRequestClose;
            Loaded += PluginUninstallDialog_Loaded;
        }

        /// <summary>
        /// 处理 ViewModel 的关闭请求
        /// </summary>
        private void OnRequestClose(object? sender, bool confirmed)
        {
            DialogResult = confirmed;
            Close();
        }

        /// <summary>
        /// 窗口加载完成
        /// </summary>
        private void PluginUninstallDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 播放进入动画
            var fadeIn = new DoubleAnimation(0, 1, System.TimeSpan.FromMilliseconds(150));
            var scaleX = new DoubleAnimation(0.96, 1, System.TimeSpan.FromMilliseconds(150));
            var scaleY = new DoubleAnimation(0.96, 1, System.TimeSpan.FromMilliseconds(150));

            fadeIn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            MainContainer.BeginAnimation(OpacityProperty, fadeIn);
            ContainerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            ContainerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
        }

        /// <summary>
        /// 标题栏拖动
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
