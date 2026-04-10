using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginHostObjectFactory
{
    PlayerApi CreatePlayerApi(PluginContext context, EventManager eventManager);

    WindowApi CreateWindowApi(PluginContext context, EventManager eventManager);

    WebViewApi CreateWebViewApi(string pluginId);

    OverlayApi CreateOverlayApi(PluginContext context, ConfigApi configApi);

    PanelApi CreatePanelApi(PluginContext context, ConfigApi configApi);

    SubtitleApi CreateSubtitleApi(PluginContext context, V8ScriptEngine engine, EventManager eventManager);

    HotkeyApi CreateHotkeyApi(string pluginId);

    OsdApi CreateOsdApi(string pluginId);
}
