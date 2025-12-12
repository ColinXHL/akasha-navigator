using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// SettingsWindow - 设置窗口
    /// </summary>
    public partial class SettingsWindow : AnimatedWindow
    {
        #region Fields

        private AppConfig _config;
        private bool _isInitializing = true;

        #endregion

        #region Events

        /// <summary>
        /// 设置保存事件
        /// </summary>
        public event EventHandler<AppConfig>? SettingsSaved;

        #endregion

        #region Constructor

        public SettingsWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;
            LoadSettings();
            _isInitializing = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载设置到 UI
        /// </summary>
        private void LoadSettings()
        {
            // 视频控制
            SeekSecondsSlider.Value = _config.SeekSeconds;
            SeekSecondsValue.Text = $"{_config.SeekSeconds}s";

            // 透明度
            OpacitySlider.Value = _config.DefaultOpacity * 100;
            OpacityValue.Text = $"{(int)(_config.DefaultOpacity * 100)}%";

            // 窗口行为
            EdgeSnapCheckBox.IsChecked = _config.EnableEdgeSnap;
            SnapThresholdSlider.Value = _config.SnapThreshold;
            SnapThresholdValue.Text = $"{_config.SnapThreshold}px";

            // Profile
            CurrentProfileText.Text = $"{ProfileManager.Instance.CurrentProfile.Icon} {ProfileManager.Instance.CurrentProfile.Name}";
        }

        /// <summary>
        /// 从 UI 读取设置
        /// </summary>
        private void SaveSettingsToConfig()
        {
            _config.SeekSeconds = (int)SeekSecondsSlider.Value;
            _config.DefaultOpacity = OpacitySlider.Value / 100.0;
            _config.EnableEdgeSnap = EdgeSnapCheckBox.IsChecked ?? true;
            _config.SnapThreshold = (int)SnapThresholdSlider.Value;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// 快进秒数滑块值变化
        /// </summary>
        private void SeekSecondsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            SeekSecondsValue.Text = $"{(int)SeekSecondsSlider.Value}s";
        }

        /// <summary>
        /// 透明度滑块值变化
        /// </summary>
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            OpacityValue.Text = $"{(int)OpacitySlider.Value}%";
        }

        /// <summary>
        /// 吸附阈值滑块值变化
        /// </summary>
        private void SnapThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            SnapThresholdValue.Text = $"{(int)SnapThresholdSlider.Value}px";
        }

        /// <summary>
        /// 打开配置文件夹
        /// </summary>
        private void BtnOpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = ProfileManager.Instance.DataDirectory;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// 重置按钮 - 恢复默认设置
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            
            // 重置为默认值
            SeekSecondsSlider.Value = AppConstants.DefaultSeekSeconds;
            SeekSecondsValue.Text = $"{AppConstants.DefaultSeekSeconds}s";
            
            OpacitySlider.Value = AppConstants.MaxOpacity * 100;
            OpacityValue.Text = $"{(int)(AppConstants.MaxOpacity * 100)}%";
            
            EdgeSnapCheckBox.IsChecked = true;
            SnapThresholdSlider.Value = AppConstants.SnapThreshold;
            SnapThresholdValue.Text = $"{AppConstants.SnapThreshold}px";
            
            _isInitializing = false;
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        /// <summary>
        /// 保存按钮
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsToConfig();
            CloseWithAnimation(() => SettingsSaved?.Invoke(this, _config));
        }

        #endregion
    }
}
