using System;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Services;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Core;

public sealed class PluginHostObjectFactory : IPluginHostObjectFactory
{
    private readonly IPlayerRuntimeBridge _runtimeBridge;
    private readonly IOverlayManager _overlayManager;
    private readonly IPanelManager _panelManager;
    private readonly ICursorDetectionService _cursorDetectionService;
    private readonly ISubtitleService _subtitleService;
    private readonly ScriptExecutionQueue _scriptExecutionQueue;
    private readonly HotkeyService _hotkeyService;
    private readonly OsdManager _osdManager;
    private readonly ILogService _logService;

    public PluginHostObjectFactory(
        IPlayerRuntimeBridge runtimeBridge,
        IOverlayManager overlayManager,
        IPanelManager panelManager,
        ICursorDetectionService cursorDetectionService,
        ISubtitleService subtitleService,
        ScriptExecutionQueue scriptExecutionQueue,
        HotkeyService hotkeyService,
        OsdManager osdManager,
        ILogService logService)
    {
        _runtimeBridge = runtimeBridge ?? throw new ArgumentNullException(nameof(runtimeBridge));
        _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
        _panelManager = panelManager ?? throw new ArgumentNullException(nameof(panelManager));
        _cursorDetectionService = cursorDetectionService ?? throw new ArgumentNullException(nameof(cursorDetectionService));
        _subtitleService = subtitleService ?? throw new ArgumentNullException(nameof(subtitleService));
        _scriptExecutionQueue = scriptExecutionQueue ?? throw new ArgumentNullException(nameof(scriptExecutionQueue));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _osdManager = osdManager ?? throw new ArgumentNullException(nameof(osdManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public PlayerApi CreatePlayerApi(PluginContext context, EventManager eventManager)
    {
        var playerApi = new PlayerApi(context, _runtimeBridge.GetPlayerWindow);
        playerApi.SetEventManager(eventManager);
        return playerApi;
    }

    public WindowApi CreateWindowApi(PluginContext context, EventManager eventManager)
    {
        var windowApi = new WindowApi(context, _runtimeBridge, _cursorDetectionService);
        windowApi.SetEventManager(eventManager);
        return windowApi;
    }

    public WebViewApi CreateWebViewApi(string pluginId)
    {
        return new WebViewApi(pluginId, _runtimeBridge, _scriptExecutionQueue, _logService);
    }

    public OverlayApi CreateOverlayApi(PluginContext context, ConfigApi configApi)
    {
        return new OverlayApi(context, configApi, _overlayManager);
    }

    public PanelApi CreatePanelApi(PluginContext context, ConfigApi configApi)
    {
        return new PanelApi(context, configApi, _panelManager, _runtimeBridge);
    }

    public SubtitleApi CreateSubtitleApi(PluginContext context, V8ScriptEngine engine, EventManager eventManager)
    {
        var subtitleApi = new SubtitleApi(context, engine, _subtitleService);
        subtitleApi.SetEventManager(eventManager);
        return subtitleApi;
    }

    public HotkeyApi CreateHotkeyApi(string pluginId)
    {
        return new HotkeyApi(pluginId, _hotkeyService, _hotkeyService.GetDispatcher());
    }

    public OsdApi CreateOsdApi(string pluginId)
    {
        return new OsdApi(pluginId, _osdManager);
    }
}
