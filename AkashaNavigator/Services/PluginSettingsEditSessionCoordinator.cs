using System;
using System.Collections.Generic;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services;

public class PluginSettingsEditSessionCoordinator : IPluginSettingsEditSessionCoordinator
{
    private sealed class SessionState
    {
        public Window? ParentPluginCenter { get; init; }

        public Window? ParentSettings { get; init; }

        public OverlayWindow OverlayWindow { get; init; } = null!;

        public EventHandler? ExitHandler { get; init; }
    }

    private readonly Dictionary<PluginSettingsWindow, SessionState> _sessions = new();

    public void EnterOverlayEditSession(PluginSettingsWindow window, OverlayWindow overlayWindow)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (overlayWindow == null)
            throw new ArgumentNullException(nameof(overlayWindow));

        if (_sessions.ContainsKey(window))
        {
            ExitOverlayEditSession(window);
        }

        Window? pluginCenter = null;
        Window? settings = null;

        foreach (Window current in Application.Current.Windows)
        {
            if (current == window)
                continue;

            if (current is PluginCenterWindow && current.IsVisible)
            {
                pluginCenter = current;
                current.Hide();
                continue;
            }

            if (current is SettingsWindow && current.IsVisible)
            {
                settings = current;
                current.Hide();
            }
        }

        EventHandler exitHandler = (_, _) => ExitOverlayEditSession(window);
        overlayWindow.EditModeExited += exitHandler;

        _sessions[window] = new SessionState {
            ParentPluginCenter = pluginCenter,
            ParentSettings = settings,
            OverlayWindow = overlayWindow,
            ExitHandler = exitHandler
        };

        window.Hide();
        overlayWindow.Show();
        overlayWindow.EnterEditMode();
    }

    public void ExitOverlayEditSession(PluginSettingsWindow window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        if (!_sessions.TryGetValue(window, out var sessionState))
            return;

        if (sessionState.ExitHandler != null)
        {
            sessionState.OverlayWindow.EditModeExited -= sessionState.ExitHandler;
        }

        if (sessionState.ParentSettings != null)
        {
            sessionState.ParentSettings.Show();
        }

        if (sessionState.ParentPluginCenter != null)
        {
            sessionState.ParentPluginCenter.Show();
        }

        _sessions.Remove(window);

        window.Show();
        window.Activate();
    }
}
