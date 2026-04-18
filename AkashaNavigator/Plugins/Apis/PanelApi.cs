using System;
using System.Collections.Generic;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Panel API
/// 提供插件独立普通窗口能力
/// </summary>
public class PanelApi
{
    private readonly PluginContext _context;
    private readonly ConfigApi _configApi;
    private readonly IPanelManager _panelManager;
    private readonly IPlayerRuntimeBridge? _runtimeBridge;
    private readonly EventManager _eventManager;

    private PluginPanelWindow? _boundPanel;

    public PanelApi(PluginContext context, ConfigApi configApi, IPanelManager panelManager,
                    IPlayerRuntimeBridge? runtimeBridge = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configApi = configApi ?? throw new ArgumentNullException(nameof(configApi));
        _panelManager = panelManager ?? throw new ArgumentNullException(nameof(panelManager));
        _runtimeBridge = runtimeBridge;
        _eventManager = new EventManager();
    }

    public void on(string eventName, object callback)
    {
        _eventManager.On(eventName, callback);
    }

    public void off(string eventName)
    {
        _eventManager.Off(eventName);
    }

    public void show()
    {
        try
        {
            var panel = EnsurePanel();
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (panel == null)
                    return;

                // 将面板绑定到播放器窗口，确保面板始终位于播放器之上
                if (panel.Owner == null)
                {
                    var playerWindow = _runtimeBridge?.GetPlayerWindow();
                    if (playerWindow != null)
                    {
                        panel.Owner = playerWindow;
                        panel.ShowActivated = false;
                    }
                }

                panel.Show();
            });
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void hide()
    {
        try
        {
            var panel = _panelManager.GetPanel(_context.PluginId);
            System.Windows.Application.Current?.Dispatcher.Invoke(() => panel?.Hide());
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void close()
    {
        try
        {
            _panelManager.DestroyPanel(_context.PluginId);
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void setPosition(double x, double y)
    {
        try
        {
            var panel = EnsurePanel();
            System.Windows.Application.Current?.Dispatcher.Invoke(() => panel?.SetPosition(x, y));
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void setSize(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return;

        var panel = EnsurePanel();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => panel?.SetSize(width, height));
    }

    public void setTopmost(bool enabled)
    {
        var panel = EnsurePanel();
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  if (panel != null)
                                                                      panel.Topmost = enabled;
                                                              });
    }

    public object? getPosition()
    {
        var panel = _panelManager.GetPanel(_context.PluginId);
        if (panel == null)
            return null;

        object? result = null;
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              { result = new { x = panel.Left, y = panel.Top }; });
        return result;
    }

    public object? getBounds()
    {
        var panel = _panelManager.GetPanel(_context.PluginId);
        if (panel == null)
            return null;

        object? result = null;
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            result = new { x = panel.Left, y = panel.Top, width = panel.Width, height = panel.Height };
        });
        return result;
    }

    public PanelContext getContext()
    {
        EnsurePanel();
        return new PanelContext(_context.PluginId, _panelManager);
    }

    public void setHeader(string title, string? hint = null)
    {
        var panel = EnsurePanel();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => panel?.SetHeader(title, hint));
    }

    public void setPageList(object pages, int currentPage = 1)
    {
        var panel = EnsurePanel();
        if (panel == null)
            return;

        var items = ParsePageItems(pages, currentPage);
        System.Windows.Application.Current?.Dispatcher.Invoke(() => panel.SetPageItems(items));
    }

    public void setActionButtons(object? options)
    {
        var panel = EnsurePanel();
        if (panel == null || options == null)
            return;

        var dict = JsTypeConverter.ToDictionary(options);
        dict.TryGetValue("prev", out var prevRaw);
        dict.TryGetValue("next", out var nextRaw);
        dict.TryGetValue("danmaku", out var danmakuRaw);
        dict.TryGetValue("subtitle", out var subtitleRaw);
        dict.TryGetValue("prevEnabled", out var prevEnabledRaw);
        dict.TryGetValue("nextEnabled", out var nextEnabledRaw);
        dict.TryGetValue("danmakuEnabled", out var danmakuEnabledRaw);
        dict.TryGetValue("subtitleEnabled", out var subtitleEnabledRaw);

        var prev = prevRaw?.ToString();
        var next = nextRaw?.ToString();
        var danmaku = danmakuRaw?.ToString();
        var subtitle = subtitleRaw?.ToString();
        var prevEnabled = ParseOptionalBool(prevEnabledRaw);
        var nextEnabled = ParseOptionalBool(nextEnabledRaw);
        var danmakuEnabled = ParseOptionalBool(danmakuEnabledRaw);
        var subtitleEnabled = ParseOptionalBool(subtitleEnabledRaw);

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            panel.SetNavigationButtonLabels(prev, next);
            panel.SetActionButtonLabels(danmaku, subtitle);
            panel.SetNavigationButtonStates(prevEnabled, nextEnabled);
            panel.SetActionButtonStates(danmakuEnabled, subtitleEnabled);
        });
    }

    private static bool? ParseOptionalBool(object? value)
    {
        if (value == null)
            return null;

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            if (bool.TryParse(value.ToString(), out var parsed))
                return parsed;
            return null;
        }
    }

    private Views.Windows.PluginPanelWindow? EnsurePanel()
    {
        var panel = _panelManager.GetPanel(_context.PluginId);
        if (panel == null)
        {
            var x = Convert.ToDouble(_configApi.Get("panel.x", _configApi.Get("overlay.x", (object)120.0)));
            var y = Convert.ToDouble(_configApi.Get("panel.y", _configApi.Get("overlay.y", (object)120.0)));
            var width = Convert.ToDouble(_configApi.Get("panel.width", (object)360.0));
            var height = Convert.ToDouble(_configApi.Get("panel.height", (object)460.0));
            var topmost = Convert.ToBoolean(_configApi.Get("panel.topmost", (object)false));

            var options = new PanelOptions {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Topmost = topmost,
                Title = _context.Manifest.Name ?? _context.PluginId
            };

            panel = _panelManager.CreatePanel(_context.PluginId, options);
        }

        if (panel != null && !ReferenceEquals(_boundPanel, panel))
        {
            BindPanelEvents(panel);
            _boundPanel = panel;
        }

        return panel;
    }

    private void BindPanelEvents(Views.Windows.PluginPanelWindow panel)
    {
        panel.IsVisibleChanged += (_, _) =>
        {
            _eventManager.Emit("visibilityChanged", new { isVisible = panel.IsVisible });
            if (!panel.IsVisible)
            {
                _eventManager.Emit("hide", new { isVisible = false });
            }
        };

        panel.LocationChanged += (_, _) =>
        {
            _eventManager.Emit("move", new { x = panel.Left, y = panel.Top });
        };

        panel.SizeChanged += (_, _) =>
        {
            _eventManager.Emit("resize", new { width = panel.Width, height = panel.Height });
        };

        panel.Closed += (_, _) => { _eventManager.Emit("close"); };

        panel.CanvasClicked += (_, point) => { _eventManager.Emit("click", new { x = point.X, y = point.Y }); };

        panel.PageItemClicked += (_, item) =>
        {
            _eventManager.Emit("pageClick", new { page = item.Page, cid = item.Cid, part = item.Part });
        };

        panel.ActionButtonClicked += (_, action) => { _eventManager.Emit("actionClick", new { action }); };
    }

    private static List<PanelPageItem> ParsePageItems(object pages, int currentPage)
    {
        var result = new List<PanelPageItem>();
        var rawList = JsTypeConverter.FromJs<List<object?>>(pages);
        if (rawList == null)
            return result;

        foreach (var raw in rawList)
        {
            var dict = JsTypeConverter.ToDictionary(raw);
            if (!dict.TryGetValue("page", out var pageRaw))
                continue;

            int page;
            try
            {
                page = Convert.ToInt32(pageRaw);
            }
            catch
            {
                continue;
            }

            var part = dict.TryGetValue("part", out var partRaw) ? (partRaw?.ToString() ?? $"P{page}") : $"P{page}";

            var duration = 0;
            if (dict.TryGetValue("duration", out var durationRaw) && durationRaw != null)
            {
                try
                {
                    duration = Convert.ToInt32(durationRaw);
                }
                catch
                {
                    duration = 0;
                }
            }

            long cid = 0;
            if (dict.TryGetValue("cid", out var cidRaw) && cidRaw != null)
            {
                try
                {
                    cid = Convert.ToInt64(cidRaw);
                }
                catch
                {
                    cid = 0;
                }
            }

            result.Add(new PanelPageItem {
                Page = page,
                Cid = cid,
                Part = part,
                DurationSeconds = duration,
                IsActive = page == currentPage
            });
        }

        result.Sort((a, b) => a.Page.CompareTo(b.Page));
        return result;
    }
}
}
