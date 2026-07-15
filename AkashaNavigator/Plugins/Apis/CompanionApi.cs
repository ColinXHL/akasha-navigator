using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Services.Companion;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis;

public sealed class CompanionApi
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.Ordinal)
    {
        "worker.echo",
        "worker.getStatus",
        "worker.shutdown",
        "automation.emergencyStop"
    };

    private readonly PluginContext _context;
    private readonly ICompanionProcessManager _processManager;
    private readonly ILogService _logService;

    public CompanionApi(
        PluginContext context,
        ICompanionProcessManager processManager,
        ILogService logService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    [ScriptMember("start")]
    public async Task<object> Start()
    {
        try
        {
            var manifest = _context.Manifest.Companion
                           ?? throw new InvalidOperationException("Plugin manifest has no companion declaration.");
            var status = await _processManager.StartAsync(
                    _context.PluginId,
                    _context.PluginDirectory,
                    manifest)
                .ConfigureAwait(false);
            return new { success = true, status = JsTypeConverter.ToJs(status, _context.Engine) };
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CompanionApi), ex,
                              "Failed to start companion for plugin {PluginId}", _context.PluginId);
            return new { success = false, error = ex.Message };
        }
    }

    [ScriptMember("invoke")]
    public async Task<object> Invoke(string method, object? payload = null)
    {
        if (!AllowedMethods.Contains(method))
        {
            return new { success = false, error = $"Companion method '{method}' is not allowed." };
        }

        try
        {
            var normalizedPayload = NormalizePayload(payload);
            var result = await _processManager.InvokeAsync(
                    _context.PluginId,
                    method,
                    normalizedPayload)
                .ConfigureAwait(false);
            return new { success = true, data = JsTypeConverter.ToJs(result, _context.Engine) };
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CompanionApi), ex,
                              "Companion invocation failed for plugin {PluginId}, method {Method}",
                              _context.PluginId, method);
            return new { success = false, error = ex.Message };
        }
    }

    [ScriptMember("getStatus")]
    public object GetStatus()
    {
        var status = _processManager.GetStatus(_context.PluginId);
        return JsTypeConverter.ToJs(status, _context.Engine)!;
    }

    [ScriptMember("stop")]
    public async Task<object> Stop()
    {
        try
        {
            await _processManager.StopAsync(_context.PluginId).ConfigureAwait(false);
            return new { success = true };
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(CompanionApi), ex,
                              "Failed to stop companion for plugin {PluginId}", _context.PluginId);
            return new { success = false, error = ex.Message };
        }
    }

    private static JsonElement? NormalizePayload(object? payload)
    {
        if (payload == null || payload is Undefined)
        {
            return null;
        }

        object normalized = payload is ScriptObject or PropertyBag
            ? JsTypeConverter.ToDictionary(payload)
            : payload;
        return JsonSerializer.SerializeToElement(normalized, CompanionProtocol.JsonOptions);
    }
}
