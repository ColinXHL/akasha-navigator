using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Services;

public sealed class PluginPermissionConsentService : IPluginPermissionConsentService
{
    private const int CurrentSchemaVersion = 1;

    private readonly IDialogFactory? _dialogFactory;
    private readonly ILogService? _logService;
    private readonly string _consentFilePath;
    private readonly Func<PluginManifest, IReadOnlyCollection<string>, PluginPermissionConsentOperation, bool>?
        _confirmationOverride;
    private readonly object _syncRoot = new();

    public PluginPermissionConsentService(IDialogFactory dialogFactory, ILogService logService)
        : this(AppPaths.PluginPermissionConsentsFilePath, dialogFactory, logService, null)
    {
    }

    internal PluginPermissionConsentService(
        string consentFilePath,
        Func<PluginManifest, IReadOnlyCollection<string>, PluginPermissionConsentOperation, bool>
            confirmationOverride)
        : this(consentFilePath, null, null, confirmationOverride)
    {
    }

    private PluginPermissionConsentService(
        string consentFilePath,
        IDialogFactory? dialogFactory,
        ILogService? logService,
        Func<PluginManifest, IReadOnlyCollection<string>, PluginPermissionConsentOperation, bool>?
            confirmationOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consentFilePath);
        _consentFilePath = Path.GetFullPath(consentFilePath);
        _dialogFactory = dialogFactory;
        _logService = logService;
        _confirmationOverride = confirmationOverride;
    }

    public bool EnsureHighRiskPermissionsApproved(
        PluginManifest manifest,
        PluginPermissionConsentOperation operation)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var permissions = PluginPermissions.GetHighRiskPermissions(manifest.Permissions);
        if (permissions.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            return false;
        }

        var pluginKey = NormalizePluginId(manifest.Id);
        var fingerprint = CreatePermissionFingerprint(manifest, permissions);

        lock (_syncRoot)
        {
            if (!TryLoadConsentDocument(out var document))
            {
                return false;
            }

            if (document.Grants.TryGetValue(pluginKey, out var existing) &&
                string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return true;
            }

            if (!ShowConfirmation(manifest, permissions, operation))
            {
                return false;
            }

            document.Grants[pluginKey] = new PluginPermissionGrant
            {
                Fingerprint = fingerprint,
                ApprovedAtUtc = DateTimeOffset.UtcNow
            };
            return TrySaveConsentDocument(document);
        }
    }

    public bool RevokeHighRiskPermissionConsent(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (_syncRoot)
        {
            if (!TryLoadConsentDocument(out var document))
            {
                return false;
            }

            if (!document.Grants.Remove(NormalizePluginId(pluginId)))
            {
                return true;
            }

            return TrySaveConsentDocument(document);
        }
    }

    internal static string CreatePermissionFingerprint(
        PluginManifest manifest,
        IReadOnlyCollection<string>? highRiskPermissions = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var permissions = (highRiskPermissions ?? PluginPermissions.GetHighRiskPermissions(manifest.Permissions))
            .Select(permission => permission.ToLowerInvariant())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var canonical = new
        {
            pluginId = NormalizePluginId(manifest.Id ?? string.Empty),
            permissions,
            companion = permissions.Contains(PluginPermissions.Companion, StringComparer.Ordinal)
                ? new
                {
                    executable = manifest.Companion?.Executable?.Replace('\\', '/'),
                    protocolVersion = manifest.Companion?.ProtocolVersion,
                    lifetime = manifest.Companion?.Lifetime,
                    singleInstance = manifest.Companion?.SingleInstance,
                    shutdownTimeoutMs =
                        manifest.Companion?.ShutdownTimeoutMs
                }
                : null
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private bool ShowConfirmation(
        PluginManifest manifest,
        IReadOnlyCollection<string> permissions,
        PluginPermissionConsentOperation operation)
    {
        if (_confirmationOverride != null)
        {
            return _confirmationOverride(manifest, permissions, operation);
        }

        var application = Application.Current;
        if (application == null || _dialogFactory == null)
        {
            return false;
        }

        bool ShowDialog()
        {
            var action = operation switch
            {
                PluginPermissionConsentOperation.Install => "安装",
                PluginPermissionConsentOperation.Update => "更新",
                PluginPermissionConsentOperation.FirstEnable => "启用",
                _ => "继续"
            };
            var permissionLines = string.Join(
                Environment.NewLine,
                permissions.Select(permission => $"• {DescribePermission(permission)}"));
            var companionDetails =
                permissions.Contains(
                    PluginPermissions.Companion,
                    StringComparer.OrdinalIgnoreCase)
                    ? $"{Environment.NewLine}{Environment.NewLine}" +
                      $"将启动可执行文件：{manifest.Companion?.Executable ?? "未声明"}{Environment.NewLine}" +
                      $"协议版本：{manifest.Companion?.ProtocolVersion}"
                    : string.Empty;
            var message =
                $"插件“{manifest.Name ?? manifest.Id ?? "未知插件"}”请求以下高风险权限：{Environment.NewLine}{Environment.NewLine}" +
                $"{permissionLines}{companionDetails}{Environment.NewLine}{Environment.NewLine}" +
                "仅在你信任插件来源时允许。Akasha 会限制可执行文件路径和启动参数，但该进程仍能在当前用户权限下运行。";
            var dialog = _dialogFactory.CreateConfirmDialog(
                message,
                "高风险插件权限",
                $"允许并{action}",
                "取消");
            if (application.MainWindow?.IsVisible == true)
            {
                dialog.Owner = application.MainWindow;
            }

            dialog.ShowDialog();
            return dialog.Result == true;
        }

        return application.Dispatcher.CheckAccess()
            ? ShowDialog()
            : application.Dispatcher.Invoke(ShowDialog);
    }

    private bool TryLoadConsentDocument(out PluginPermissionConsentDocument document)
    {
        if (!File.Exists(_consentFilePath))
        {
            document = new PluginPermissionConsentDocument();
            return true;
        }

        try
        {
            var json = File.ReadAllText(_consentFilePath);
            document = JsonSerializer.Deserialize<PluginPermissionConsentDocument>(json, JsonHelper.ReadOptions)
                       ?? throw new JsonException("Permission consent document deserialized to null.");
            if (document.SchemaVersion != CurrentSchemaVersion || document.Grants == null)
            {
                throw new JsonException("Permission consent document schema is not supported.");
            }

            if (document.Grants.Any(pair =>
                    string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Value == null ||
                    string.IsNullOrWhiteSpace(pair.Value.Fingerprint)))
            {
                throw new JsonException("Permission consent document contains an invalid grant.");
            }

            document.Grants = document.Grants.ToDictionary(
                pair => NormalizePluginId(pair.Key),
                pair => pair.Value,
                StringComparer.Ordinal);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            _logService?.Error(nameof(PluginPermissionConsentService), ex,
                               "Failed to load plugin permission consent file {FilePath}", _consentFilePath);
            document = new PluginPermissionConsentDocument();
            return false;
        }
    }

    private bool TrySaveConsentDocument(PluginPermissionConsentDocument document)
    {
        var temporaryPath = $"{_consentFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_consentFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(document, JsonHelper.WriteOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _consentFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService?.Error(nameof(PluginPermissionConsentService), ex,
                               "Failed to save plugin permission consent file {FilePath}", _consentFilePath);
            return false;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }
        }
    }

    private static string NormalizePluginId(string pluginId) => pluginId.Trim().ToLowerInvariant();

    private static string DescribePermission(string permission)
    {
        return permission.Equals(PluginPermissions.Companion, StringComparison.OrdinalIgnoreCase)
            ? "companion：启动并通信于插件包内随附的独立程序"
            : permission;
    }

    private sealed class PluginPermissionConsentDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public Dictionary<string, PluginPermissionGrant> Grants { get; set; } =
            new(StringComparer.Ordinal);
    }

    private sealed class PluginPermissionGrant
    {
        public string Fingerprint { get; set; } = string.Empty;

        public DateTimeOffset ApprovedAtUtc { get; set; }
    }
}
