using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace AkashaNavigator.Views.Windows;

public partial class PluginSettingsWindow : AnimatedWindow
{
    private readonly PluginSettingsViewModel _viewModel;
    private readonly IPluginSettingsEditSessionCoordinator _editSessionCoordinator;
    private readonly ILogService _logService;

    private SettingsUiRenderer? _renderer;

    public PluginSettingsWindow(
        PluginSettingsViewModel viewModel,
        IPluginSettingsEditSessionCoordinator editSessionCoordinator,
        ILogService logService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _editSessionCoordinator = editSessionCoordinator ?? throw new ArgumentNullException(nameof(editSessionCoordinator));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        InitializeComponent();

        DataContext = _viewModel;
        TitleText.Text = $"{_viewModel.PluginName} - 设置";
        Title = $"{_viewModel.PluginName} - 设置";

        RenderSettings();
    }

    private void RenderSettings()
    {
        SettingsContainer.Children.Clear();

        if (_viewModel.SettingsDefinition == null)
        {
            var noSettingsText = new System.Windows.Controls.TextBlock {
                Text = "此插件没有可配置的设置项",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            SettingsContainer.Children.Add(noSettingsText);
            return;
        }

        _renderer = new SettingsUiRenderer(_viewModel.SettingsDefinition, _viewModel.Config);
        _renderer.ValueChanged += OnSettingValueChanged;
        _renderer.ButtonAction += OnButtonAction;

        var settingsPanel = _renderer.Render();
        SettingsContainer.Children.Add(settingsPanel);
    }

    private void OnSettingValueChanged(object? sender, SettingsValueChangedEventArgs e)
    {
        _viewModel.UpdateValue(e.Key, e.Value);
    }

    private void OnButtonAction(object? sender, SettingsButtonActionEventArgs e)
    {
        HandleButtonAction(e.Action);
    }

    private void HandleButtonAction(string action)
    {
        if (string.IsNullOrEmpty(action))
            return;

        switch (action)
        {
            case SettingsButtonActions.EnterEditMode:
                EnterOverlayEditMode();
                break;

            case SettingsButtonActions.ResetConfig:
                ResetToDefaults();
                break;

            case SettingsButtonActions.OpenPluginFolder:
                OpenPluginFolder();
                break;

            default:
                _viewModel.NotifyAction(action);
                break;
        }
    }

    private void EnterOverlayEditMode()
    {
        if (!_viewModel.IsCurrentProfileActive())
        {
            _viewModel.ShowWarning("请先激活此 Profile 后再调整覆盖层位置");
            return;
        }

        var overlayManager = App.Services.GetRequiredService<IOverlayManager>();
        var overlay = overlayManager.GetOverlay(_viewModel.PluginId);

        if (overlay == null)
        {
            var x = _viewModel.Config.Get("overlay.x", 100.0);
            var y = _viewModel.Config.Get("overlay.y", 100.0);
            var size = _viewModel.Config.Get("overlay.size", 200.0);

            var options = new OverlayOptions { X = x, Y = y, Width = size, Height = size };
            overlay = overlayManager.CreateOverlay(_viewModel.PluginId, options);
        }

        overlay.EditModeExited += OnOverlayEditModeExited;
        _editSessionCoordinator.EnterOverlayEditSession(this, overlay);
    }

    private async void OnOverlayEditModeExited(object? sender, EventArgs e)
    {
        if (sender is not OverlayWindow overlay)
            return;

        overlay.EditModeExited -= OnOverlayEditModeExited;

        _viewModel.UpdateValue("overlay.x", overlay.Left);
        _viewModel.UpdateValue("overlay.y", overlay.Top);
        _viewModel.UpdateValue("overlay.size", overlay.Width);

        _renderer?.RefreshValues();

        await _viewModel.SaveAsync(notifyUser: false, reloadPlugin: false);
    }

    private void OpenPluginFolder()
    {
        try
        {
            if (Directory.Exists(_viewModel.PluginDirectory))
            {
                Process.Start(new ProcessStartInfo { FileName = _viewModel.PluginDirectory, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PluginSettingsWindow), ex, "打开插件目录失败");
        }
    }

    private void ResetToDefaults()
    {
        if (_viewModel.SettingsDefinition?.Sections == null)
            return;

        foreach (var section in _viewModel.SettingsDefinition.Sections)
        {
            if (section.Items == null)
                continue;

            foreach (var item in section.Items)
            {
                ResetItemToDefault(item);
            }
        }

        _renderer?.RefreshValues();
        _viewModel.MarkDirty();
    }

    private void ResetItemToDefault(SettingsItem item)
    {
        if (string.IsNullOrEmpty(item.Key))
            return;

        if (item.Default.HasValue)
        {
            var defaultValue = item.Default.Value;
            switch (defaultValue.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetString());
                    break;
                case System.Text.Json.JsonValueKind.Number:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetDouble());
                    break;
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                    _viewModel.UpdateValue(item.Key, defaultValue.GetBoolean());
                    break;
            }
        }
        else
        {
            _viewModel.RemoveValue(item.Key);
        }

        if (item.Items == null)
            return;

        foreach (var subItem in item.Items)
        {
            ResetItemToDefault(subItem);
        }
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        ResetToDefaults();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        CloseWithAnimation();
    }

    public static bool HasSettingsUi(string pluginDirectory)
    {
        if (string.IsNullOrEmpty(pluginDirectory))
            return false;

        var settingsUiPath = Path.Combine(pluginDirectory, "settings_ui.json");
        return File.Exists(settingsUiPath);
    }
}
