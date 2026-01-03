using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Dialogs;

namespace AkashaNavigator.Views.Dialogs
{
    /// <summary>
    /// 卸载确认对话框 - 显示关联的 Profile 列表并确认卸载
    /// </summary>
    public partial class UninstallConfirmDialog : AnimatedWindow
    {
        private readonly UninstallConfirmDialogViewModel _viewModel;
        private readonly string _pluginId;
        private readonly string? _pluginName;

        /// <summary>
        /// 卸载是否成功
        /// </summary>
        public bool UninstallSucceeded => _viewModel.UninstallSucceeded;

        /// <summary>
        /// 错误信息（如果卸载失败）
        /// </summary>
        public string? ErrorMessage => _viewModel.ErrorMessage;

        /// <summary>
        /// 构造函数
        /// </summary>
        public UninstallConfirmDialog(
            UninstallConfirmDialogViewModel viewModel,
            string pluginId,
            string? pluginName = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
            _pluginName = pluginName;

            InitializeComponent();
            DataContext = _viewModel;

            // 初始化 ViewModel
            _viewModel.Initialize(pluginId, pluginName);

            // 订阅关闭请求事件
            _viewModel.RequestClose += OnRequestClose;

            Loaded += UninstallConfirmDialog_Loaded;

            // 根据内容调整窗口高度
            AdjustWindowHeight();
        }

        /// <summary>
        /// 根据是否有引用 Profile 调整窗口高度
        /// </summary>
        private void AdjustWindowHeight()
        {
            if (_viewModel.HasReferencingProfiles)
            {
                var baseHeight = 280;
                var profileHeight = Math.Min(_viewModel.ReferencingProfiles.Count * 40, 120);
                Height = baseHeight + profileHeight;
            }
            else
            {
                Height = 200;
            }
        }

        /// <summary>
        /// 窗口加载完成
        /// </summary>
        private void UninstallConfirmDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 播放进入动画
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            var scaleX = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(150));
            var scaleY = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(150));

            fadeIn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            MainContainer.BeginAnimation(OpacityProperty, fadeIn);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
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
