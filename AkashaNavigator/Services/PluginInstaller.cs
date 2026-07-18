using System.IO;
using System.Reflection;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Services;

/// <summary>
/// 将 catalog Manifest v2 转换为当前宿主格式并提交到插件库。
/// </summary>
public sealed class PluginInstaller : IPluginInstaller
{
    private readonly IPluginRepositoryService _repositoryService;
    private readonly IPluginSubscriptionService _subscriptionService;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly ILogService? _logService;
    private readonly Func<string> _hostVersionProvider;
    private readonly object _installLock = new();

    public PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        ILogService logService)
        : this(
            repositoryService,
            subscriptionService,
            pluginLibrary,
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
            hostVersionProvider,
            null)
    {
    }

    private PluginInstaller(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        Func<string> hostVersionProvider,
        ILogService? logService)
    {
        _repositoryService =
            repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _subscriptionService =
            subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _hostVersionProvider =
            hostVersionProvider ?? throw new ArgumentNullException(nameof(hostVersionProvider));
        _logService = logService;
    }

    public Result<InstalledPluginInfo> InstallOrUpdateRepositoryPlugin(
        string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Validation(
                    "PLUGIN_INSTALL_ID_INVALID",
                    "插件 ID 无效"));
        }

        lock (_installLock)
        {
            return InstallOrUpdateRepositoryPluginCore(pluginId);
        }
    }

    public Result<InstalledPluginInfo> InstallPackage(string archivePath)
    {
        lock (_installLock)
        {
            return _pluginLibrary.InstallPluginPackage(archivePath);
        }
    }

    private Result<InstalledPluginInfo> InstallOrUpdateRepositoryPluginCore(
        string pluginId)
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

        if (!string.Equals(
                entry.DistributionType,
                AppConstants.PluginDistributionRepository,
                StringComparison.Ordinal))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.DistributionUnsupported,
                    $"当前阶段尚不支持 {entry.DistributionType} 分发",
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
        var validation = ValidateManifest(entry, manifest, sourceDirectory);
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

        var preparationDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.RepositoryPlugin.{pluginId}.{Guid.NewGuid():N}");
        try
        {
            CopyDirectorySecure(sourceDirectory, preparationDirectory);
            var runtimeManifest = manifest.ToRuntimeManifest();
            var saveResult = JsonHelper.SaveToFile(
                Path.Combine(
                    preparationDirectory,
                    AppConstants.PluginManifestFileName),
                runtimeManifest);
            if (saveResult.IsFailure)
            {
                return Result<InstalledPluginInfo>.Failure(saveResult.Error!);
            }

            var installResult = _pluginLibrary.InstallOrUpdateFromDirectory(
                preparationDirectory,
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
        finally
        {
            TryDeleteDirectory(preparationDirectory);
        }
    }

    private Error? ValidateManifest(
        PluginRepositoryEntry entry,
        CatalogPluginManifest manifest,
        string sourceDirectory)
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
                AppConstants.PluginDistributionRepository,
                StringComparison.Ordinal) ||
            manifest.Backend != null)
        {
            return InvalidManifest(entry.Id, "Manifest v2 与 repo.json 不一致");
        }

        if (PluginLibrary.CompareVersions(
                _hostVersionProvider(),
                manifest.Host.MinVersion) < 0)
        {
            return Error.Plugin(
                PluginErrorCodes.HostVersionTooLow,
                $"插件需要 AkashaNavigator {manifest.Host.MinVersion} 或更高版本",
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

        if (!ValidateRequiredFile(sourceDirectory, manifest.Main) ||
            (!string.IsNullOrWhiteSpace(manifest.Settings) &&
             !ValidateRequiredFile(sourceDirectory, manifest.Settings)))
        {
            return InvalidManifest(entry.Id, "插件入口或设置文件不存在");
        }

        if (manifest.Library == null ||
            manifest.Library.Distinct(StringComparer.Ordinal).Count() !=
            manifest.Library.Count ||
            manifest.Library.Any(
                path => !ValidateRequiredDirectory(sourceDirectory, path)))
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

    private static void CopyDirectorySecure(
        string sourceDirectory,
        string targetDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException(sourceDirectory);
        }

        if ((source.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("插件目录不能是重解析点");
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in source.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"插件包含重解析文件: {file.Name}");
            }

            file.CopyTo(Path.Combine(targetDirectory, file.Name), overwrite: true);
        }

        foreach (var directory in source.EnumerateDirectories())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"插件包含重解析目录: {directory.Name}");
            }

            CopyDirectorySecure(
                directory.FullName,
                Path.Combine(targetDirectory, directory.Name));
        }
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

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // 准备目录由系统后续清理。
        }
    }
}
