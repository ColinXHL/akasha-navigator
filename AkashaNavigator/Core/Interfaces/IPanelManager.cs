using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core.Interfaces
{
/// <summary>
/// 插件独立面板窗口管理服务接口
/// </summary>
public interface IPanelManager
{
    PluginPanelWindow CreatePanel(string pluginId, PanelOptions? options = null);

    PluginPanelWindow? GetPanel(string pluginId);

    void DestroyPanel(string pluginId);

    void DestroyAllPanels();
}
}
