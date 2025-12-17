using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;
using Microsoft.Win32;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 我的 Profile 页面 - 显示 Profile 详情和插件清单
    /// </summary>
    public partial class MyProfilesPage : UserControl
    {
        private string? _currentProfileId;

        public MyProfilesPage()
        {
            InitializeComponent();
            Loaded += MyProfilesPage_Loaded;
        }

        private void MyProfilesPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshProfileList();
        }

        /// <summary>
        /// 刷新 Profile 列表
        /// </summary>
        public void RefreshProfileList()
        {
            var profiles = ProfileManager.Instance.Profiles;
            var currentProfile = ProfileManager.Instance.CurrentProfile;

            // 填充 ComboBox
            ProfileSelector.Items.Clear();
            foreach (var profile in profiles)
            {
                ProfileSelector.Items.Add(new ComboBoxItem
                {
                    Content = profile.Name,
                    Tag = profile.Id
                });
            }

            // 选中当前 Profile
            for (int i = 0; i < ProfileSelector.Items.Count; i++)
            {
                if (ProfileSelector.Items[i] is ComboBoxItem item && 
                    item.Tag is string id && 
                    id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ProfileSelector.SelectedIndex = i;
                    break;
                }
            }

            // 如果没有选中，选择第一个
            if (ProfileSelector.SelectedIndex < 0 && ProfileSelector.Items.Count > 0)
            {
                ProfileSelector.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 刷新插件清单
        /// </summary>
        public void RefreshPluginList()
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                PluginList.ItemsSource = null;
                NoPluginsText.Visibility = Visibility.Visible;
                MissingWarning.Visibility = Visibility.Collapsed;
                PluginCountText.Text = "(0 个插件)";
                return;
            }

            // 获取 Profile 的插件引用
            var references = PluginAssociationManager.Instance.GetPluginsInProfile(_currentProfileId);
            
            // 获取缺失插件
            var missingPlugins = PluginAssociationManager.Instance.GetMissingPlugins(_currentProfileId);

            // 显示缺失警告
            if (missingPlugins.Count > 0)
            {
                MissingWarning.Visibility = Visibility.Visible;
                MissingWarningText.Text = $"{missingPlugins.Count} 个插件缺失，部分功能可能无法使用";
            }
            else
            {
                MissingWarning.Visibility = Visibility.Collapsed;
            }

            // 转换为视图模型
            var viewModels = references.Select(r => CreatePluginViewModel(r)).ToList();

            PluginList.ItemsSource = viewModels;
            PluginCountText.Text = $"({viewModels.Count} 个插件)";
            NoPluginsText.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 创建插件视图模型
        /// </summary>
        private ProfilePluginViewModel CreatePluginViewModel(PluginReference reference)
        {
            var vm = new ProfilePluginViewModel
            {
                PluginId = reference.PluginId,
                Enabled = reference.Enabled,
                Status = reference.Status
            };

            // 获取插件信息
            if (reference.Status == PluginInstallStatus.Installed || 
                reference.Status == PluginInstallStatus.Disabled)
            {
                var manifest = PluginLibrary.Instance.GetPluginManifest(reference.PluginId);
                if (manifest != null)
                {
                    vm.Name = manifest.Name ?? reference.PluginId;
                    vm.Version = manifest.Version ?? "1.0.0";
                    vm.Description = manifest.Description;
                }
                else
                {
                    vm.Name = reference.PluginId;
                    vm.Version = "?";
                }
            }
            else
            {
                // 缺失的插件，尝试从内置插件获取信息
                vm.Name = reference.PluginId;
                vm.Version = "?";
                
                var builtInPath = Path.Combine(AppPaths.BuiltInPluginsDirectory, reference.PluginId, "plugin.json");
                var result = PluginManifest.LoadFromFile(builtInPath);
                if (result.IsSuccess && result.Manifest != null)
                {
                    vm.Name = result.Manifest.Name ?? reference.PluginId;
                    vm.Version = result.Manifest.Version ?? "1.0.0";
                    vm.Description = result.Manifest.Description;
                }
            }

            return vm;
        }

        /// <summary>
        /// Profile 选择变化
        /// </summary>
        private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileSelector.SelectedItem is ComboBoxItem item && item.Tag is string profileId)
            {
                _currentProfileId = profileId;
                RefreshPluginList();
                UpdateProfileButtons();
            }
        }

        /// <summary>
        /// 更新 Profile 操作按钮状态
        /// </summary>
        private void UpdateProfileButtons()
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                BtnEditProfile.IsEnabled = false;
                BtnDeleteProfile.IsEnabled = false;
                return;
            }

            // 编辑按钮始终可用
            BtnEditProfile.IsEnabled = true;

            // 删除按钮对默认 Profile 禁用
            var isDefault = ProfileManager.Instance.IsDefaultProfile(_currentProfileId);
            BtnDeleteProfile.IsEnabled = !isDefault;
        }

        /// <summary>
        /// 插件启用/禁用切换
        /// </summary>
        private void PluginToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
            {
                var enabled = checkBox.IsChecked ?? false;
                ProfileManager.Instance.SetPluginEnabled(_currentProfileId, pluginId, enabled);
                
                // 刷新列表以更新状态显示
                RefreshPluginList();
            }
        }

        /// <summary>
        /// 添加插件按钮点击
        /// </summary>
        private void BtnAddPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                MessageBox.Show("请先选择一个 Profile", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取已安装但未添加到当前 Profile 的插件
            var installedPlugins = PluginLibrary.Instance.GetInstalledPlugins();
            var currentPlugins = PluginAssociationManager.Instance.GetPluginsInProfile(_currentProfileId)
                .Select(r => r.PluginId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var availablePlugins = installedPlugins
                .Where(p => !currentPlugins.Contains(p.Id))
                .ToList();

            if (availablePlugins.Count == 0)
            {
                MessageBox.Show("没有可添加的插件。\n\n请先在「已安装插件」页面安装插件。", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 显示插件选择对话框
            var dialog = new PluginSelectorDialog(availablePlugins, _currentProfileId);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                RefreshPluginList();
            }
        }

        /// <summary>
        /// 安装单个缺失插件
        /// </summary>
        private void BtnInstallPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                var result = PluginLibrary.Instance.InstallPlugin(pluginId);
                if (result.IsSuccess)
                {
                    RefreshPluginList();
                }
                else
                {
                    MessageBox.Show($"安装失败: {result.ErrorMessage}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 一键安装缺失插件
        /// </summary>
        private void BtnInstallMissing_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfileId))
                return;

            var missingPlugins = PluginAssociationManager.Instance.GetMissingPlugins(_currentProfileId);
            if (missingPlugins.Count == 0)
            {
                MessageBox.Show("没有缺失的插件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var errors = new List<string>();

            foreach (var pluginId in missingPlugins)
            {
                var result = PluginLibrary.Instance.InstallPlugin(pluginId);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    errors.Add($"{pluginId}: {result.ErrorMessage}");
                }
            }

            RefreshPluginList();

            if (failCount > 0)
            {
                MessageBox.Show($"安装完成\n\n成功: {successCount} 个\n失败: {failCount} 个\n\n失败详情:\n{string.Join("\n", errors)}", 
                    "安装结果", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"成功安装 {successCount} 个插件", "安装完成", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 从 Profile 移除插件
        /// </summary>
        private void BtnRemovePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId && !string.IsNullOrEmpty(_currentProfileId))
            {
                var result = MessageBox.Show(
                    $"确定要从此 Profile 中移除插件 \"{pluginId}\" 吗？\n\n注意：这只会移除引用，不会卸载插件本体。",
                    "确认移除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    PluginAssociationManager.Instance.RemovePluginFromProfile(pluginId, _currentProfileId);
                    RefreshPluginList();
                }
            }
        }

        /// <summary>
        /// 新建 Profile 按钮点击
        /// </summary>
        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileCreateDialog();
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true && dialog.IsConfirmed && !string.IsNullOrEmpty(dialog.ProfileId))
            {
                // 刷新 Profile 列表
                RefreshProfileList();
                
                // 选中新创建的 Profile
                SelectProfile(dialog.ProfileId);
            }
        }

        /// <summary>
        /// 编辑 Profile 按钮点击
        /// </summary>
        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                MessageBox.Show("请先选择一个 Profile", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var profile = ProfileManager.Instance.GetProfileById(_currentProfileId);
            if (profile == null)
            {
                MessageBox.Show("Profile 不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new ProfileEditDialog(profile);
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true && dialog.IsConfirmed)
            {
                // 刷新 Profile 列表以显示更新后的名称
                RefreshProfileList();
            }
        }

        /// <summary>
        /// 删除 Profile 按钮点击
        /// </summary>
        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                MessageBox.Show("请先选择一个 Profile", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 检查是否是默认 Profile
            if (ProfileManager.Instance.IsDefaultProfile(_currentProfileId))
            {
                MessageBox.Show("默认 Profile 不能删除", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var profile = ProfileManager.Instance.GetProfileById(_currentProfileId);
            var profileName = profile?.Name ?? _currentProfileId;

            // 显示确认对话框
            var result = MessageBox.Show(
                $"确定要删除 Profile \"{profileName}\" 吗？\n\n此操作将删除该 Profile 及其所有插件关联，但不会卸载插件本体。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 执行删除
            var deleteResult = ProfileManager.Instance.DeleteProfile(_currentProfileId);
            
            if (deleteResult.IsSuccess)
            {
                // 刷新 Profile 列表（会自动切换到默认 Profile）
                RefreshProfileList();
                MessageBox.Show($"Profile \"{profileName}\" 已删除", "删除成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"删除失败: {deleteResult.ErrorMessage}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 选中指定的 Profile
        /// </summary>
        private void SelectProfile(string profileId)
        {
            for (int i = 0; i < ProfileSelector.Items.Count; i++)
            {
                if (ProfileSelector.Items[i] is ComboBoxItem item && 
                    item.Tag is string id && 
                    id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
                {
                    ProfileSelector.SelectedIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// 导出 Profile
        /// </summary>
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                MessageBox.Show("请先选择一个 Profile", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var profile = ProfileManager.Instance.GetProfileById(_currentProfileId);
            if (profile == null)
            {
                MessageBox.Show("Profile 不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出 Profile",
                Filter = "JSON 文件 (*.json)|*.json",
                FileName = $"{profile.Name ?? _currentProfileId}_profile.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                var success = ProfileManager.Instance.ExportProfileToFile(_currentProfileId, dialog.FileName);
                if (success)
                {
                    MessageBox.Show($"Profile 已导出到:\n{dialog.FileName}", "导出成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("导出失败，请查看日志获取详细信息", "导出失败", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 导入 Profile
        /// </summary>
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入 Profile",
                Filter = "JSON 文件 (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
                return;

            // 加载导入数据
            var data = ProfileExportData.LoadFromFile(dialog.FileName);
            if (data == null)
            {
                MessageBox.Show("无法解析导入文件，请确保文件格式正确", "导入失败", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 预览导入
            var preview = ProfileManager.Instance.PreviewImport(data);
            
            // 构建确认消息
            var message = $"即将导入 Profile: {data.ProfileName}\n\n";
            message += $"包含 {data.PluginReferences.Count} 个插件引用\n";
            
            if (preview.MissingPlugins.Count > 0)
            {
                message += $"\n⚠ {preview.MissingPlugins.Count} 个插件缺失:\n";
                message += string.Join("\n", preview.MissingPlugins.Take(5).Select(p => $"  • {p}"));
                if (preview.MissingPlugins.Count > 5)
                {
                    message += $"\n  ... 等 {preview.MissingPlugins.Count} 个";
                }
                message += "\n\n导入后可以一键安装缺失插件。";
            }

            bool overwrite = false;
            if (preview.ProfileExists)
            {
                var overwriteResult = MessageBox.Show(
                    $"Profile \"{data.ProfileId}\" 已存在。\n\n是否覆盖现有 Profile？",
                    "Profile 已存在",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (overwriteResult == MessageBoxResult.Cancel)
                    return;
                
                overwrite = overwriteResult == MessageBoxResult.Yes;
                if (!overwrite)
                {
                    MessageBox.Show("导入已取消", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                var confirmResult = MessageBox.Show(message, "确认导入", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmResult != MessageBoxResult.Yes)
                    return;
            }

            // 执行导入
            var importResult = ProfileManager.Instance.ImportProfile(data, overwrite);
            
            if (importResult.IsSuccess)
            {
                RefreshProfileList();
                
                // 选中导入的 Profile
                for (int i = 0; i < ProfileSelector.Items.Count; i++)
                {
                    if (ProfileSelector.Items[i] is ComboBoxItem item && 
                        item.Tag is string id && 
                        id.Equals(data.ProfileId, StringComparison.OrdinalIgnoreCase))
                    {
                        ProfileSelector.SelectedIndex = i;
                        break;
                    }
                }

                var successMessage = $"Profile \"{data.ProfileName}\" 导入成功！";
                if (importResult.MissingPlugins.Count > 0)
                {
                    successMessage += $"\n\n有 {importResult.MissingPlugins.Count} 个插件缺失，可以点击「一键安装缺失插件」进行安装。";
                }
                
                MessageBox.Show(successMessage, "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"导入失败: {importResult.ErrorMessage}", "导入失败", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }

    /// <summary>
    /// Profile 插件视图模型
    /// </summary>
    public class ProfilePluginViewModel
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;
        public PluginInstallStatus Status { get; set; } = PluginInstallStatus.Installed;

        /// <summary>
        /// 是否可以切换启用状态（缺失的插件不能切换）
        /// </summary>
        public bool CanToggle => Status != PluginInstallStatus.Missing;

        /// <summary>
        /// 是否有描述
        /// </summary>
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        /// <summary>
        /// 描述可见性
        /// </summary>
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 安装按钮可见性（仅缺失时显示）
        /// </summary>
        public Visibility InstallButtonVisibility => Status == PluginInstallStatus.Missing ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText => Status switch
        {
            PluginInstallStatus.Installed => "已安装",
            PluginInstallStatus.Missing => "缺失",
            PluginInstallStatus.Disabled => "已禁用",
            _ => "未知"
        };

        /// <summary>
        /// 状态颜色
        /// </summary>
        public Brush StatusColor => Status switch
        {
            PluginInstallStatus.Installed => new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)), // 绿色
            PluginInstallStatus.Missing => new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)),   // 红色
            PluginInstallStatus.Disabled => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), // 灰色
            _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };

        /// <summary>
        /// 状态标签样式
        /// </summary>
        public Style StatusTagStyle
        {
            get
            {
                var style = new Style(typeof(Border));
                style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(3)));
                style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(6, 2, 6, 2)));
                
                var bgColor = Status switch
                {
                    PluginInstallStatus.Installed => Color.FromRgb(0x1A, 0x3A, 0x1A),
                    PluginInstallStatus.Missing => Color.FromRgb(0x3A, 0x1A, 0x1A),
                    PluginInstallStatus.Disabled => Color.FromRgb(0x2A, 0x2A, 0x2A),
                    _ => Color.FromRgb(0x2A, 0x2A, 0x2A)
                };
                style.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(bgColor)));
                
                return style;
            }
        }
    }
}
