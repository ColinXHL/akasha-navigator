using System;
using System.Collections.Generic;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services
{
/// <summary>
/// 插件独立窗口管理服务
/// </summary>
public class PanelManager : IPanelManager
{
    private static readonly Lazy<PanelManager> _instance = new(() => new PanelManager());

    public static PanelManager Instance => _instance.Value;

    private readonly Dictionary<string, PluginPanelWindow> _panels = new();
    private readonly object _lock = new();

    private PanelManager()
    {
    }

    public PluginPanelWindow CreatePanel(string pluginId, PanelOptions? options = null)
    {
        lock (_lock)
        {
            if (_panels.TryGetValue(pluginId, out var existing))
            {
                existing.Close();
                _panels.Remove(pluginId);
            }

            var panel = new PluginPanelWindow(pluginId);

            if (options != null)
            {
                if (!string.IsNullOrWhiteSpace(options.Title))
                    panel.Title = options.Title;

                if (options.X.HasValue && options.Y.HasValue)
                    panel.SetPosition(options.X.Value, options.Y.Value);

                if (options.Width.HasValue && options.Height.HasValue)
                    panel.SetSize(options.Width.Value, options.Height.Value);

                if (options.Topmost.HasValue)
                    panel.Topmost = options.Topmost.Value;
            }

            _panels[pluginId] = panel;
            return panel;
        }
    }

    public PluginPanelWindow? GetPanel(string pluginId)
    {
        lock (_lock)
        {
            return _panels.TryGetValue(pluginId, out var panel) ? panel : null;
        }
    }

    public void DestroyPanel(string pluginId)
    {
        lock (_lock)
        {
            if (_panels.TryGetValue(pluginId, out var panel))
            {
                Application.Current?.Dispatcher.Invoke(() =>
                                                       {
                                                           panel.Hide();
                                                           panel.Close();
                                                       });

                _panels.Remove(pluginId);
            }
        }
    }

    public void DestroyAllPanels()
    {
        lock (_lock)
        {
            foreach (var panel in _panels.Values)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                                                       {
                                                           panel.Hide();
                                                           panel.Close();
                                                       });
            }

            _panels.Clear();
        }
    }
}

/// <summary>
/// 插件独立窗口创建选项
/// </summary>
public class PanelOptions
{
    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public string? Title { get; set; }

    public bool? Topmost { get; set; }
}
}
