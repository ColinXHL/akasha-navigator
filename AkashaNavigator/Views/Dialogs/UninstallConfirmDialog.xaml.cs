using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// 卸载确认对话框 - 显示关联的Profile列表并确认卸载
/// </summary>
public partial class UninstallConfirmDialog : AnimatedWindow
{
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly INotificationService _notificationService;
    private readonly ILogService _logService;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly string _pluginId;
    private readonly string _pluginName;
    private readonly List<string> _referencingProfiles;

    /// <summary>
    /// 卸载是否成功
    /// </summary>
    public bool UninstallSucceeded { get; private set; }

    /// <summary>
    /// 错误信息（如果卸载失败）
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public UninstallConfirmDialog(
        IPluginAssociationManager pluginAssociationManager,
        INotificationService notificationService,
        ILogService logService,
        IPluginLibrary pluginLibrary,
        string pluginId,
        string? pluginName = null)
    {
        _pluginAssociationManager = pluginAssociationManager;
        _notificationService = notificationService;
        _logService = logService;
        _pluginLibrary = pluginLibrary;
        InitializeComponent();

        _pluginId = pluginId;
        _pluginName = pluginName ?? pluginId;
        _referencingProfiles = _pluginAssociationManager.GetProfilesUsingPlugin(pluginId);

        InitializeUI();
        Loaded += UninstallConfirmDialog_Loaded;
    }

    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        // 设置插件名称
        PluginNameText.Text = $"确定要卸载 \"{_pluginName}\" 吗？";

        // 根据是否有引用显示不同内容
        if (_referencingProfiles.Count > 0)
        {
            WarningPanel.Visibility = Visibility.Visible;
            ProfileListScroller.Visibility = Visibility.Visible;
            ConsequenceText.Visibility = Visibility.Visible;
            ProfileList.ItemsSource = _referencingProfiles;

            // 调整窗口高度以适应内容
            var baseHeight = 280;
            var profileHeight = System.Math.Min(_referencingProfiles.Count * 40, 120);
            Height = baseHeight + profileHeight;
        }
        else
        {
            // 无引用时显示简单确认
            WarningPanel.Visibility = Visibility.Collapsed;
            ProfileListScroller.Visibility = Visibility.Collapsed;
            ConsequenceText.Visibility = Visibility.Collapsed;
            Height = 200;
        }
    }

    /// <summary>
    /// 窗口加载完成
    /// </summary>
    private void UninstallConfirmDialog_Loaded(object sender, RoutedEventArgs e)
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

    /// <summary>
    /// 取消按钮点击
    /// </summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 确认卸载按钮点击
    /// </summary>
    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        // 执行卸载
        var result = PerformUninstall();

        if (result)
        {
            UninstallSucceeded = true;
            DialogResult = true;
        }
        else
        {
            // 使用 NotificationService 显示错误
            _notificationService.Error(ErrorMessage ?? "卸载失败，请稍后重试。", "卸载失败");
        }
    }

    /// <summary>
    /// 执行卸载操作
    /// </summary>
    /// <returns>是否成功</returns>
    private bool PerformUninstall()
    {
        // 1. 如果有关联的Profile，先清理关联关系
        if (_referencingProfiles.Count > 0)
        {
            var removedCount = _pluginAssociationManager.RemovePluginFromAllProfiles(_pluginId);
            _logService.Info("UninstallConfirmDialog",
                                     "已从 {RemovedCount} 个 Profile 中移除插件 {PluginId} 的引用", removedCount,
                                     _pluginId);
        }

        // 2. 执行卸载（强制模式，因为关联已清理）
        var uninstallResult = _pluginLibrary.UninstallPlugin(_pluginId, force: true);

        if (uninstallResult.IsSuccess)
        {
            _logService.Info("UninstallConfirmDialog", "插件 {PluginId} 卸载成功", _pluginId);
            return true;
        }
        else
        {
            ErrorMessage = uninstallResult.ErrorMessage;
            _logService.Error("UninstallConfirmDialog", "插件 {PluginId} 卸载失败: {ErrorMessage}", _pluginId,
                                      ErrorMessage ?? "未知错误");
            return false;
        }
    }
}
}
