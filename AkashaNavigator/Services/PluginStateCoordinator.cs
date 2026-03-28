using System;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 协调插件状态变化，将底层服务事件统一桥接为 UI 刷新事件。
/// </summary>
public sealed class PluginStateCoordinator : IDisposable
{
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly IEventBus _eventBus;
    private readonly ILogService _logService;
    private bool _disposed;

    public PluginStateCoordinator(IPluginLibrary pluginLibrary, IPluginAssociationManager pluginAssociationManager,
                                  IEventBus eventBus, ILogService logService)
    {
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _pluginAssociationManager =
            pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        _pluginLibrary.PluginChanged += OnPluginLibraryChanged;
        _pluginAssociationManager.AssociationChanged += OnAssociationChanged;
        _pluginAssociationManager.PluginEnabledChanged += OnPluginEnabledChanged;
    }

    private void OnPluginLibraryChanged(object? sender, PluginLibraryChangedEventArgs e)
    {
        _logService.Debug(nameof(PluginStateCoordinator),
                          "桥接插件库变化事件: {ChangeType}, PluginId={PluginId}", e.ChangeType, e.PluginId);
        PublishRefreshEvents();
    }

    private void OnAssociationChanged(object? sender, AssociationChangedEventArgs e)
    {
        _logService.Debug(nameof(PluginStateCoordinator),
                          "桥接插件关联变化事件: {ChangeType}, PluginId={PluginId}, ProfileId={ProfileId}",
                          e.ChangeType, e.PluginId, e.ProfileId);
        PublishRefreshEvents();
    }

    private void OnPluginEnabledChanged(object? sender, PluginEnabledChangedEventArgs e)
    {
        _logService.Debug(nameof(PluginStateCoordinator),
                          "桥接插件启用状态变化事件: PluginId={PluginId}, ProfileId={ProfileId}, Enabled={Enabled}",
                          e.PluginId, e.ProfileId, e.Enabled);
        PublishRefreshEvents();
    }

    private void PublishRefreshEvents()
    {
        _eventBus.Publish(new PluginListChangedEvent());
        _eventBus.Publish(new ProfileListChangedEvent());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _pluginLibrary.PluginChanged -= OnPluginLibraryChanged;
        _pluginAssociationManager.AssociationChanged -= OnAssociationChanged;
        _pluginAssociationManager.PluginEnabledChanged -= OnPluginEnabledChanged;
        _disposed = true;
    }
}
}
