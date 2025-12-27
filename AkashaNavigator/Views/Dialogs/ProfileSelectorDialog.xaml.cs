using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Views.Dialogs
{
/// <summary>
/// Profile选择器对话框 - 用于将插件添加到选定的Profile
/// </summary>
public partial class ProfileSelectorDialog : AnimatedWindow
{
    private readonly IProfileManager _profileManager;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly INotificationService _notificationService;
    private readonly ILogService _logService;
    private readonly string _pluginId;
    private List<ProfileSelectionItem> _profiles = new();

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public ProfileSelectorDialog(
        IProfileManager profileManager,
        IPluginAssociationManager pluginAssociationManager,
        INotificationService notificationService,
        ILogService logService,
        string pluginId)
    {
        _profileManager = profileManager;
        _pluginAssociationManager = pluginAssociationManager;
        _notificationService = notificationService;
        _logService = logService;
        _pluginId = pluginId;
        InitializeComponent();
        LoadProfiles();
    }

    /// <summary>
    /// 加载Profile列表
    /// </summary>
    private void LoadProfiles()
    {
        var allProfiles = _profileManager.InstalledProfiles;
        var profilesWithPlugin = _pluginAssociationManager.GetProfilesUsingPlugin(_pluginId);
        var profilesWithPluginSet = profilesWithPlugin.ToHashSet();

        _profiles = allProfiles
                        .Select(p => new ProfileSelectionItem { Id = p.Id, Name = p.Name ?? p.Id, Icon = p.Icon ?? "📋",
                                                                IsSelected = false,
                                                                AlreadyAdded = profilesWithPluginSet.Contains(p.Id),
                                                                CanSelect = !profilesWithPluginSet.Contains(p.Id) })
                        .ToList();

        ProfileList.ItemsSource = _profiles;
        UpdateSelectionCount();
    }

    /// <summary>
    /// 更新选择计数
    /// </summary>
    private void UpdateSelectionCount()
    {
        var selectedCount = _profiles.Count(p => p.IsSelected);
        SelectionCountText.Text = $"已选择 {selectedCount} 个 Profile";
        BtnConfirm.IsEnabled = selectedCount > 0;
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <summary>
    /// Profile复选框状态变化
    /// </summary>
    private void ProfileCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCount();
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
    /// 确定按钮点击
    /// </summary>
    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var selectedProfiles = _profiles.Where(p => p.IsSelected).Select(p => p.Id).ToList();

        if (selectedProfiles.Count == 0)
        {
            _notificationService.Warning("请至少选择一个 Profile", "提示");
            return;
        }

        // 添加插件到选定的Profile
        int successCount = 0;
        foreach (var profileId in selectedProfiles)
        {
            try
            {
                _pluginAssociationManager.AddPluginToProfile(_pluginId, profileId);
                successCount++;
            }
            catch (Exception ex)
            {
                _logService.Error("ProfileSelectorDialog",
                                          "添加插件到 Profile {ProfileId} 失败: {ErrorMessage}", profileId, ex.Message);
            }
        }

        if (successCount > 0)
        {
            _notificationService.Success($"已成功将插件添加到 {successCount} 个 Profile", "添加成功");
            DialogResult = true;
        }
        else
        {
            _notificationService.Error("添加失败，请查看日志了解详情", "添加失败");
            DialogResult = false;
        }

        Close();
    }
}

/// <summary>
/// Profile选择项视图模型
/// </summary>
public class ProfileSelectionItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "📋";
    public bool IsSelected { get; set; }
    public bool AlreadyAdded { get; set; }
    public bool CanSelect { get; set; } = true;
    public Visibility AlreadyAddedVisibility => AlreadyAdded ? Visibility.Visible : Visibility.Collapsed;
}
}
