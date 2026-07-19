using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// 将 catalog Manifest v2 转换为当前宿主格式并提交到插件库。
/// </summary>
public sealed class PluginInstaller : IPluginInstaller
{
    private readonly IPluginRepositoryService _repositoryService;
    private readonly IPluginSubscriptionService _subscriptionService;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IPluginDistributionResolver _distributionResolver;
    private readonly ILogService? _logService;
    private readonly Func<string> _hostVersionProvider;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        IPluginDistributionResolver distributionResolver,
        ILogService logService)
        : this(
            repositoryService,
            subscriptionService,
            pluginLibrary,
            distributionResolver,
            GetCurrentHostVersion,
            logService)
    {
    }

    internal PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        Func<string> hostVersionProvider)
        : this(
            repositoryService,
            subscriptionService,
            pluginLibrary,
            PluginDistributionResolver.CreateUnavailable(),
            hostVersionProvider,
            null)
    {
    }

    internal PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        IPluginPackageService pluginPackageService,
        Func<string> hostVersionProvider)
        : this(
            repositoryService,
            subscriptionService,
            pluginLibrary,
            new PluginDistributionResolver(pluginPackageService),
            hostVersionProvider,
            null)
    {
    }

    private PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        IPluginDistributionResolver distributionResolver,
        Func<string> hostVersionProvider,
        ILogService? logService)
    {
        _repositoryService =
            repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _subscriptionService =
            subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _distributionResolver =
            distributionResolver ??
            throw new ArgumentNullException(nameof(distributionResolver));
        _hostVersionProvider =
            hostVersionProvider ?? throw new ArgumentNullException(nameof(hostVersionProvider));
        _logService = logService;
    }

    public Result<InstalledPluginInfo> InstallOrUpdateRepositoryPlugin(
        string pluginId)
    {
        return InstallOrUpdateRepositoryPluginAsync(pluginId)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<Result<InstalledPluginInfo>>
        InstallOrUpdateRepositoryPluginAsync(
            string pluginId,
            IProgress<PluginDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Validation(
                    "PLUGIN_INSTALL_ID_INVALID",
                    "插件 ID 无效"));
        }

        await _installGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await InstallOrUpdateRepositoryPluginCoreAsync(
                    pluginId,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _installGate.Release();
        }
    }

    public Result<InstalledPluginInfo> InstallPackage(string archivePath)
    {
        _installGate.Wait();
        try
        {
            return _pluginLibrary.InstallPluginPackage(archivePath);
        }
        finally
        {
            _installGate.Release();
        }
    }

    private async Task<Result<InstalledPluginInfo>>
        InstallOrUpdateRepositoryPluginCoreAsync(
            string pluginId,
            IProgress<PluginDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        var snapshot = _repositoryService.Current;
        var entry = snapshot?.Index.Plugins.FirstOrDefault(
            item => string.Equals(
                item.Id,
                pluginId,
                StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.RepositoryPluginNotFound,
                    $"插件仓库中不存在 {pluginId}",
                    pluginId: pluginId));
        }

        var sourceDirectory = GetContainedPluginDirectory(
            _repositoryService.RepositoryDirectory,
            entry);
        var manifestResult = JsonHelper.LoadFromFile<CatalogPluginManifest>(
            Path.Combine(
                sourceDirectory,
                AppConstants.PluginRepositoryManifestFileName));
        if (manifestResult.IsFailure)
        {
            return Result<InstalledPluginInfo>.Failure(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        var isRelease = string.Equals(
            entry.DistributionType,
            AppConstants.PluginDistributionRelease,
            StringComparison.Ordinal);
        var validation = ValidateManifest(
            entry,
            manifest,
            sourceDirectory,
            validatePayload: !isRelease);
        if (validation != null)
        {
            return Result<InstalledPluginInfo>.Failure(validation);
        }

        var subscriptionResult = _subscriptionService.Subscribe(
            AppConstants.OfficialPluginRepositoryId,
            entry);
        if (subscriptionResult.IsFailure)
        {
            return Result<InstalledPluginInfo>.Failure(subscriptionResult.Error!);
        }

        try
        {
            var distributionResult =
                await _distributionResolver.ResolveAsync(
                        pluginId,
                        entry,
                        manifest,
                        sourceDirectory,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (distributionResult.IsFailure)
            {
                return Result<InstalledPluginInfo>.Failure(
                    distributionResult.Error!);
            }

            using var distribution = distributionResult.Value!;
            var installSourceDirectory = distribution.SourceDirectory;
            if (isRelease)
            {
                var packageManifestResult =
                    JsonHelper.LoadFromFile<CatalogPluginManifest>(
                        Path.Combine(
                            installSourceDirectory,
                            AppConstants.PluginRepositoryManifestFileName));
                if (packageManifestResult.IsFailure)
                {
                    return Result<InstalledPluginInfo>.Failure(
                        packageManifestResult.Error!);
                }

                var packageManifest = packageManifestResult.Value!;
                var packageValidation = ValidateReleasePackageManifest(
                    entry,
                    manifest,
                    packageManifest,
                    installSourceDirectory);
                if (packageValidation != null)
                {
                    return Result<InstalledPluginInfo>.Failure(
                        packageValidation);
                }

                manifest = packageManifest;
            }

            var runtimeManifest = manifest.ToRuntimeManifest();
            var saveResult = JsonHelper.SaveToFile(
                Path.Combine(
                    installSourceDirectory,
                    AppConstants.PluginManifestFileName),
                runtimeManifest);
            if (saveResult.IsFailure)
            {
                return Result<InstalledPluginInfo>.Failure(saveResult.Error!);
            }

            var installResult = _pluginLibrary.InstallOrUpdateFromDirectory(
                installSourceDirectory,
                manifest.SavedFiles,
                AppConstants.PluginInstallSourceRepository);
            if (installResult.IsFailure)
            {
                return installResult;
            }

            var markResult = _subscriptionService.MarkInstalled(
                pluginId,
                installResult.Value!.Version,
                snapshot!.CatalogCommit);
            if (markResult?.IsFailure == true)
            {
                _logService?.Warn(
                    nameof(PluginInstaller),
                    "插件已安装，但保存订阅安装状态失败: {ErrorMessage}",
                    markResult.Error?.Message ?? "未知错误");
            }

            return installResult;
        }
        catch (Exception ex)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.InstallTransactionFailed,
                    $"准备仓库插件失败: {ex.Message}",
                    ex,
                    sourceDirectory));
        }
    }

    private Error? ValidateManifest(
        PluginRepositoryEntry entry,
        CatalogPluginManifest manifest,
        string sourceDirectory,
        bool validatePayload,
        bool requireReleaseIntegrity = true)
    {
        if (manifest.ManifestVersion != 2 ||
            !string.Equals(manifest.Id, entry.Id, StringComparison.Ordinal) ||
            !string.Equals(manifest.Version, entry.Version, StringComparison.Ordinal) ||
            !string.Equals(manifest.Name, entry.Name, StringComparison.Ordinal) ||
            !string.Equals(
                manifest.Description,
                entry.Description,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            string.IsNullOrWhiteSpace(manifest.Description) ||
            manifest.Authors == null ||
            manifest.Authors.Count == 0 ||
            manifest.Authors.Any(author => string.IsNullOrWhiteSpace(author?.Name)) ||
            manifest.Host == null ||
            string.IsNullOrWhiteSpace(manifest.Host.MinVersion) ||
            !string.Equals(
                manifest.Host.MinVersion,
                entry.MinHostVersion,
                StringComparison.Ordinal) ||
            manifest.DefaultConfig == null ||
            manifest.Profiles == null ||
            manifest.Tags == null ||
            manifest.Distribution == null ||
            !string.Equals(
                manifest.Distribution.Type,
                entry.DistributionType,
                StringComparison.Ordinal) ||
            (entry.DistributionType != AppConstants.PluginDistributionRepository &&
             entry.DistributionType != AppConstants.PluginDistributionRelease) ||
            entry.HasBackend != (manifest.Backend != null))
        {
            return InvalidManifest(entry.Id, "Manifest v2 与 repo.json 不一致");
        }

        if (entry.DistributionType == AppConstants.PluginDistributionRepository &&
            manifest.Backend != null)
        {
            return InvalidManifest(
                entry.Id,
                "带后端插件必须使用 Release 分发");
        }

        if (entry.DistributionType == AppConstants.PluginDistributionRelease)
        {
            var distributionError = ValidateReleaseDistribution(
                entry.Id,
                entry.Version,
                manifest.Distribution,
                requireReleaseIntegrity);
            if (distributionError != null)
            {
                return distributionError;
            }
        }

        var currentHostVersion = _hostVersionProvider();
        if (!PluginLibrary.IsHostVersionCompatible(
                currentHostVersion,
                manifest.Host.MinVersion))
        {
            return Error.Plugin(
                PluginErrorCodes.HostVersionTooLow,
                $"插件需要 AkashaNavigator {manifest.Host.MinVersion} 或更高版本" +
                $"（当前 {currentHostVersion}）",
                pluginId: entry.Id);
        }

        if (manifest.Permissions == null ||
            manifest.Permissions.Distinct(StringComparer.Ordinal).Count() !=
            manifest.Permissions.Count ||
            manifest.Permissions.Any(
                permission => !PluginPermissions.IsValidPermission(permission)))
        {
            return InvalidManifest(entry.Id, "插件权限声明无效");
        }

        if (manifest.Backend != null &&
            (!manifest.Permissions.Contains(
                 PluginPermissions.Companion,
                 StringComparer.Ordinal) ||
             !string.Equals(
                 manifest.Backend.Type,
                 AppConstants.CompanionBackendType,
                 StringComparison.Ordinal) ||
             !IsSafeRelativePath(manifest.Backend.Entry) ||
             !string.Equals(
                 Path.GetExtension(manifest.Backend.Entry),
                 ".exe",
                 StringComparison.OrdinalIgnoreCase) ||
             manifest.Backend.ProtocolVersion !=
             AppConstants.CompanionProtocolVersion ||
             !string.Equals(
                 manifest.Backend.Lifetime,
                 AppConstants.CompanionLifetimePlugin,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 manifest.Backend.IntegrityLevel,
                 AppConstants.CompanionIntegrityLevelInherit,
                 StringComparison.Ordinal) ||
             manifest.Backend.ShutdownTimeoutMs <= 0 ||
             manifest.Backend.ShutdownTimeoutMs >
             AppConstants.MaxCompanionShutdownTimeoutMs))
        {
            return InvalidManifest(entry.Id, "插件后端声明无效");
        }

        if (validatePayload &&
            (!ValidateRequiredFile(sourceDirectory, manifest.Main) ||
             (!string.IsNullOrWhiteSpace(manifest.Settings) &&
              !ValidateRequiredFile(sourceDirectory, manifest.Settings)) ||
             (manifest.Backend != null &&
              !ValidateRequiredFile(
                  sourceDirectory,
                  manifest.Backend.Entry))))
        {
            return InvalidManifest(
                entry.Id,
                "插件入口、设置文件或后端可执行文件不存在");
        }

        if (manifest.Library == null ||
            manifest.Library.Distinct(StringComparer.Ordinal).Count() !=
            manifest.Library.Count ||
            (validatePayload && manifest.Library.Any(
                path => !ValidateRequiredDirectory(sourceDirectory, path))))
        {
            return InvalidManifest(entry.Id, "插件 library 目录无效");
        }

        if (manifest.HttpAllowedUrls == null ||
            manifest.HttpAllowedUrls.Any(
                value => value == null ||
                         !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return InvalidManifest(entry.Id, "插件网络白名单无效");
        }

        manifest.SavedFiles ??= new List<string>();
        manifest.SavedFiles = manifest.SavedFiles
            .Select(NormalizeSavedFilePath)
            .ToList();
        var protectedPaths = new List<string> {
            AppConstants.PluginManifestFileName,
            AppConstants.PluginRepositoryManifestFileName,
            manifest.Main
        };
        if (!string.IsNullOrWhiteSpace(manifest.Settings))
        {
            protectedPaths.Add(manifest.Settings);
        }

        protectedPaths.AddRange(manifest.Library);
        if (manifest.SavedFiles.Distinct(StringComparer.Ordinal).Count() !=
            manifest.SavedFiles.Count ||
            manifest.SavedFiles.Any(
                savedPath =>
                    !IsSafeRelativePath(savedPath) ||
                    protectedPaths.Any(
                        protectedPath => PathsOverlap(savedPath, protectedPath))))
        {
            return InvalidManifest(
                entry.Id,
                "savedFiles 包含无效路径或覆盖插件代码");
        }

        var runtimeValidation = manifest.ToRuntimeManifest().Validate();
        return runtimeValidation.IsValid
            ? null
            : InvalidManifest(entry.Id, "转换后的运行时清单无效");
    }

    private static string NormalizeSavedFilePath(string? relativePath)
    {
        return relativePath?.TrimEnd('/') ?? string.Empty;
    }

    private Error? ValidateReleasePackageManifest(
        PluginRepositoryEntry entry,
        CatalogPluginManifest catalogManifest,
        CatalogPluginManifest packageManifest,
        string packageDirectory)
    {
        var validation = ValidateManifest(
            entry,
            packageManifest,
            packageDirectory,
            validatePayload: true,
            requireReleaseIntegrity: false);
        if (validation != null)
        {
            return validation;
        }

        if (packageManifest.Distribution == null ||
            catalogManifest.Distribution == null ||
            (!string.IsNullOrWhiteSpace(
                 packageManifest.Distribution.Sha256) &&
             !string.Equals(
                 packageManifest.Distribution.Sha256,
                 catalogManifest.Distribution.Sha256,
                 StringComparison.OrdinalIgnoreCase)) ||
            (packageManifest.Distribution.Size.HasValue &&
             packageManifest.Distribution.Size !=
             catalogManifest.Distribution.Size))
        {
            return InvalidManifest(
                entry.Id,
                "Release 包内 manifest 的完整性元数据与 catalog 不一致");
        }

        packageManifest.Distribution.Sha256 =
            catalogManifest.Distribution.Sha256;
        packageManifest.Distribution.Size =
            catalogManifest.Distribution.Size;
        var catalogJson = JsonSerializer.Serialize(
            catalogManifest,
            JsonHelper.WriteOptions);
        var packageJson = JsonSerializer.Serialize(
            packageManifest,
            JsonHelper.WriteOptions);
        return JsonNode.DeepEquals(
            JsonNode.Parse(catalogJson),
            JsonNode.Parse(packageJson))
            ? null
            : InvalidManifest(
                entry.Id,
                "Release 包内 manifest 与 catalog 不一致");
    }

    private static Error? ValidateReleaseDistribution(
        string pluginId,
        string version,
        CatalogPluginDistribution distribution,
        bool requireIntegrity)
    {
        var expectedTag = $"{pluginId}-v{version}";
        var expectedAsset = $"{pluginId}-{version}-win-x64.zip";
        if (!string.Equals(
                distribution.Tag,
                expectedTag,
                StringComparison.Ordinal) ||
            !string.Equals(
                distribution.Asset,
                expectedAsset,
                StringComparison.Ordinal) ||
            (requireIntegrity &&
             (!IsSha256(distribution.Sha256) ||
              distribution.Size is null or <= 0)) ||
            (!string.IsNullOrWhiteSpace(distribution.Sha256) &&
             !IsSha256(distribution.Sha256)) ||
            (distribution.Size.HasValue && distribution.Size <= 0))
        {
            return InvalidManifest(
                pluginId,
                "Release 标签、资源名或完整性元数据无效");
        }

        return null;
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(
                   character =>
                       character is >= '0' and <= '9' or
                           >= 'a' and <= 'f');
    }

    private static bool ValidateRequiredFile(
        string rootDirectory,
        string relativePath)
    {
        return IsSafeRelativePath(relativePath) &&
               File.Exists(GetContainedPath(rootDirectory, relativePath));
    }

    private static bool ValidateRequiredDirectory(
        string rootDirectory,
        string relativePath)
    {
        return IsSafeRelativePath(relativePath) &&
               Directory.Exists(GetContainedPath(rootDirectory, relativePath));
    }

    private static bool IsSafeRelativePath(string? relativePath)
    {
        var segments = relativePath?.Split('/');
        return !string.IsNullOrWhiteSpace(relativePath) &&
               !Path.IsPathRooted(relativePath) &&
               !relativePath.Contains('\\') &&
               !relativePath.Contains(':') &&
               segments != null &&
               segments.All(
                   segment =>
                       !string.IsNullOrWhiteSpace(segment) &&
                       segment != "." &&
                       segment != "..");
    }

    private static bool PathsOverlap(string first, string second)
    {
        var normalizedFirst = first.TrimEnd('/');
        var normalizedSecond = second.TrimEnd('/');
        return string.Equals(
                   normalizedFirst,
                   normalizedSecond,
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedFirst.StartsWith(
                   normalizedSecond + "/",
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedSecond.StartsWith(
                   normalizedFirst + "/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContainedPluginDirectory(
        string repositoryDirectory,
        PluginRepositoryEntry entry)
    {
        return GetContainedPath(repositoryDirectory, entry.Path);
    }

    private static string GetContainedPath(
        string rootDirectory,
        string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            throw new InvalidDataException($"插件路径无效: {relativePath}");
        }

        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"插件路径逃逸仓库目录: {relativePath}");
        }

        return candidate;
    }

    private static Error InvalidManifest(
        string pluginId,
        string message)
    {
        return Error.Plugin(
            PluginErrorCodes.RepositoryManifestInvalid,
            message,
            pluginId: pluginId);
    }

    private static string GetCurrentHostVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadataIndex = informational.IndexOf('+');
            return metadataIndex > 0
                ? informational[..metadataIndex]
                : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? AppConstants.Version;
    }

}
