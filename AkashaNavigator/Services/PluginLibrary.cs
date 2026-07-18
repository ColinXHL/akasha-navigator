using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
#region Event Args

/// <summary>
/// 插件库变化类型
/// </summary>
public enum PluginLibraryChangeType
{
    Installed,
    Uninstalled,
    Updated
}

/// <summary>
/// 插件库变化事件参数
/// </summary>
public class PluginLibraryChangedEventArgs : EventArgs
{
    public PluginLibraryChangeType ChangeType { get; }
    public string PluginId { get; }
    public InstalledPluginInfo? PluginInfo { get; }

    public PluginLibraryChangedEventArgs(PluginLibraryChangeType changeType, string pluginId,
                                         InstalledPluginInfo? pluginInfo = null)
    {
        ChangeType = changeType;
        PluginId = pluginId;
        PluginInfo = pluginInfo;
    }
}

#endregion

/// <summary>
/// 全局插件库管理服务
/// 负责插件本体的安装、卸载、更新
/// </summary>
public class PluginLibrary : IPluginLibrary
{
#region Properties

    /// <summary>
    /// 全局插件库目录
    /// </summary>
    public string LibraryDirectory => _libraryDirectory;

    /// <summary>
    /// 插件库索引文件路径
    /// </summary>
    public string LibraryIndexPath => _libraryIndexPath;

#endregion

#region Fields

    private PluginLibraryIndex _index;
    private readonly object _indexLock = new();
    private readonly ICompanionProcessManager _companionProcessManager;
    private readonly IPluginPermissionConsentService _permissionConsentService;
    private readonly PluginWriteCoordinator _writeCoordinator;
    private readonly string _libraryDirectory;
    private readonly string _libraryIndexPath;
    private readonly string _builtInPluginsDirectory;
    private const int MaxPackageEntries = 20_000;
    private const long MaxPackageUncompressedBytes = 512L * 1024 * 1024;

#endregion

#region Constructor

    /// <summary>
    /// DI容器使用的构造函数
    /// </summary>
    public PluginLibrary(
        ICompanionProcessManager companionProcessManager,
        IPluginPermissionConsentService permissionConsentService,
        PluginWriteCoordinator writeCoordinator)
    {
        _companionProcessManager = companionProcessManager ??
                                   throw new ArgumentNullException(nameof(companionProcessManager));
        _permissionConsentService = permissionConsentService ??
                                    throw new ArgumentNullException(nameof(permissionConsentService));
        _writeCoordinator =
            writeCoordinator ?? throw new ArgumentNullException(nameof(writeCoordinator));
        _libraryDirectory = AppPaths.InstalledPluginsDirectory;
        _libraryIndexPath = AppPaths.LibraryIndexPath;
        _builtInPluginsDirectory = AppPaths.BuiltInPluginsDirectory;
        _index = LoadIndex();
    }

    /// <summary>
    /// 用于测试的构造函数
    /// </summary>
    internal PluginLibrary(
        string libraryDirectory,
        string indexPath,
        string builtInPluginsDirectory,
        ICompanionProcessManager companionProcessManager,
        IPluginPermissionConsentService permissionConsentService)
    {
        _companionProcessManager = companionProcessManager ??
                                   throw new ArgumentNullException(nameof(companionProcessManager));
        _permissionConsentService = permissionConsentService ??
                                    throw new ArgumentNullException(nameof(permissionConsentService));
        _writeCoordinator = new PluginWriteCoordinator();
        _libraryDirectory = Path.GetFullPath(libraryDirectory);
        _libraryIndexPath = Path.GetFullPath(indexPath);
        _builtInPluginsDirectory = Path.GetFullPath(builtInPluginsDirectory);
        // 此构造函数用于测试，允许自定义路径
        _index = PluginLibraryIndex.LoadFromFile(_libraryIndexPath);
    }

#endregion

#region Index Management

    /// <summary>
    /// 加载索引文件
    /// </summary>
    private PluginLibraryIndex LoadIndex()
    {
        return PluginLibraryIndex.LoadFromFile(LibraryIndexPath);
    }

    /// <summary>
    /// 保存索引文件
    /// </summary>
    private void SaveIndex()
    {
        lock (_indexLock)
        {
            _index.SaveToFile(LibraryIndexPath);
        }
    }

    /// <summary>
    /// 重新加载索引
    /// </summary>
    public void ReloadIndex()
    {
        lock (_indexLock)
        {
            _index = LoadIndex();
        }
    }

#endregion

#region Query Methods

    /// <summary>
    /// 获取所有已安装插件
    /// </summary>
    public List<InstalledPluginInfo> GetInstalledPlugins()
    {
        lock (_indexLock)
        {
            var result = new List<InstalledPluginInfo>();

            foreach (var entry in _index.Plugins)
            {
                var manifest = GetPluginManifest(entry.Id);
                if (manifest != null)
                {
                    var info = InstalledPluginInfo.FromManifest(manifest, entry.Source);
                    info.InstalledAt = entry.InstalledAt;
                    result.Add(info);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 检查插件是否已安装
    /// </summary>
    public bool IsInstalled(string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
            return false;

        lock (_indexLock)
        {
            return _index.Plugins.Any(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 获取插件目录
    /// </summary>
    public string GetPluginDirectory(string pluginId)
    {
        var installedPath = GetContainedPluginDirectory(LibraryDirectory, pluginId);
        var builtInPath = GetContainedPluginDirectory(_builtInPluginsDirectory, pluginId);

        if (!Directory.Exists(installedPath))
            return builtInPath;

        if (!Directory.Exists(builtInPath))
            return installedPath;

        var installedVersion = GetManifestVersion(installedPath);
        var builtInVersion = GetManifestVersion(builtInPath);

        // 优先使用内置插件目录，确保开发期修改即时生效
        // 当内置版本 >= 已安装版本时使用内置目录
        return CompareVersions(builtInVersion, installedVersion) >= 0 ? builtInPath : installedPath;
    }

    private static string GetManifestVersion(string pluginDirectory)
    {
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
        var result = PluginManifest.LoadFromFile(manifestPath);
        return result.IsSuccess && result.Manifest != null ? (result.Manifest.Version ?? "0.0.0") : "0.0.0";
    }

    /// <summary>
    /// 获取插件清单
    /// </summary>
    public PluginManifest? GetPluginManifest(string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
            return null;

        var pluginDir = GetPluginDirectory(pluginId);
        var manifestPath = Path.Combine(pluginDir, "plugin.json");

        var result = PluginManifest.LoadFromFile(manifestPath);
        return result.IsSuccess &&
               string.Equals(result.Manifest?.Id, pluginId, StringComparison.OrdinalIgnoreCase)
            ? result.Manifest
            : null;
    }

    /// <summary>
    /// 获取已安装插件信息
    /// </summary>
    public InstalledPluginInfo? GetInstalledPluginInfo(string pluginId)
    {
        lock (_indexLock)
        {
            var entry =
                _index.Plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                return null;

            var manifest = GetPluginManifest(pluginId);
            if (manifest == null)
                return null;

            var info = InstalledPluginInfo.FromManifest(manifest, entry.Source);
            info.InstalledAt = entry.InstalledAt;
            return info;
        }
    }

#endregion

#region Install Methods

    /// <summary>
    /// 安装插件到全局库
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="sourceDirectory">源目录（为null时从内置插件目录查找）</param>
    /// <returns>安装结果</returns>
    public Result<InstalledPluginInfo> InstallPlugin(string pluginId, string? sourceDirectory = null)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(PluginErrorCodes.InvalidManifest, "插件 ID 格式无效", pluginId: pluginId));
        }

        // 检查是否已安装
        if (IsInstalled(pluginId))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(PluginErrorCodes.AlreadyInstalled, $"插件 {pluginId} 已安装", pluginId: pluginId));
        }

        // 确定源目录
        var sourcePath = sourceDirectory ?? GetContainedPluginDirectory(_builtInPluginsDirectory, pluginId);
        var manifestResult = PluginManifest.LoadFromFile(
            Path.Combine(sourcePath, AppConstants.PluginManifestFileName));
        if (!manifestResult.IsSuccess ||
            manifestResult.Manifest == null ||
            !string.Equals(
                manifestResult.Manifest.Id,
                pluginId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.InvalidManifest,
                    $"插件源清单与请求的 ID {pluginId} 不匹配",
                    pluginId: pluginId));
        }

        return InstallOrUpdateFromDirectory(
            sourcePath,
            Array.Empty<string>(),
            sourceDirectory == null ? "builtin" : "external");
    }

    /// <summary>
    /// 从本地 ZIP 插件包安装或更新插件
    /// </summary>
    public Result<InstalledPluginInfo> InstallPluginPackage(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) ||
            !File.Exists(archivePath) ||
            !string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.SourceNotFound,
                    "请选择存在的 ZIP 插件包",
                    filePath: archivePath));
        }

        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginPackage.{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(extractionRoot);
            ExtractPluginPackage(archivePath, extractionRoot);
            var pluginDirectory = FindPluginRoot(extractionRoot);
            VerifyPackageManifestIfPresent(pluginDirectory);

            var manifestResult = PluginManifest.LoadFromFile(Path.Combine(pluginDirectory, "plugin.json"));
            if (!manifestResult.IsSuccess || manifestResult.Manifest == null)
            {
                return Result<InstalledPluginInfo>.Failure(
                    Error.Plugin(
                        PluginErrorCodes.InvalidManifest,
                        $"插件包清单无效: {manifestResult.ErrorMessage ?? "未知错误"}"));
            }

            var manifest = manifestResult.Manifest;
            var pluginId = manifest.Id!;
            ValidatePackageEntryFiles(pluginDirectory, manifest);

            return InstallOrUpdateFromDirectory(
                pluginDirectory,
                Array.Empty<string>(),
                "external");
        }
        catch (InvalidDataException ex)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(PluginErrorCodes.InvalidPackage, ex.Message));
        }
        catch (JsonException ex)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(PluginErrorCodes.InvalidPackage, $"插件包校验清单不是有效 JSON: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.CopyFailed,
                    $"插件包导入失败: {ex.Message}",
                    ex,
                    filePath: archivePath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(extractionRoot))
                {
                    Directory.Delete(extractionRoot, recursive: true);
                }
            }
            catch
            {
                // 临时目录清理由系统后续回收，不影响安装结果。
            }
        }
    }

    public Result<InstalledPluginInfo> InstallOrUpdateFromDirectory(
        string sourceDirectory,
        IReadOnlyList<string> savedFiles,
        string source)
    {
        if (savedFiles == null)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Validation(
                    PluginErrorCodes.InvalidManifest,
                    "savedFiles 不能为空"));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Validation(
                    PluginErrorCodes.InvalidManifest,
                    "插件安装来源不能为空"));
        }

        if (string.IsNullOrWhiteSpace(sourceDirectory) ||
            !Directory.Exists(sourceDirectory))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.SourceNotFound,
                    $"插件源目录不存在: {sourceDirectory}",
                    filePath: sourceDirectory));
        }

        using var writeOperation = _writeCoordinator.Acquire();
        var manifestResult = PluginManifest.LoadFromFile(
            Path.Combine(sourceDirectory, AppConstants.PluginManifestFileName));
        if (!manifestResult.IsSuccess || manifestResult.Manifest == null)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.InvalidManifest,
                    $"清单无效: {manifestResult.ErrorMessage ?? "未知错误"}"));
        }

        var manifest = manifestResult.Manifest;
        var pluginId = manifest.Id!;
        InstalledPluginEntry? entry;
        lock (_indexLock)
        {
            entry = _index.Plugins.FirstOrDefault(
                item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        var incomingVersion = manifest.Version ?? "0.0.0";
        var isUpdate = entry != null;
        if (isUpdate && CompareVersions(incomingVersion, entry!.Version) <= 0)
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.VersionNotNewer,
                    $"插件版本 {incomingVersion} 不高于已安装版本 {entry.Version}",
                    pluginId: pluginId));
        }

        if (!_permissionConsentService.EnsureHighRiskPermissionsApproved(
                manifest,
                isUpdate
                    ? PluginPermissionConsentOperation.Update
                    : PluginPermissionConsentOperation.Install))
        {
            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.PermissionConsentDeclined,
                    "未获得或无法保存插件所需的高风险权限授权",
                    pluginId: pluginId));
        }

        var targetDirectory = GetContainedPluginDirectory(LibraryDirectory, pluginId);
        var stagingDirectory = Path.Combine(
            LibraryDirectory,
            $".package-staging-{pluginId}-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(
            LibraryDirectory,
            $".package-backup-{pluginId}-{Guid.NewGuid():N}");
        var previousVersion = entry?.Version;
        var previousSource = entry?.Source;
        var addedEntry = false;
        var targetMovedToBackup = false;
        var stagingMovedToTarget = false;

        try
        {
            Directory.CreateDirectory(LibraryDirectory);
            CopyDirectory(sourceDirectory, stagingDirectory);
            if (isUpdate)
            {
                RestoreSavedFiles(
                    targetDirectory,
                    stagingDirectory,
                    savedFiles);
            }

            StopCompanionBeforeFileMutation(pluginId);

            if (Directory.Exists(targetDirectory))
            {
                Directory.Move(targetDirectory, backupDirectory);
                targetMovedToBackup = true;
            }

            Directory.Move(stagingDirectory, targetDirectory);
            stagingMovedToTarget = true;

            lock (_indexLock)
            {
                if (entry == null)
                {
                    entry = new InstalledPluginEntry {
                        Id = pluginId,
                        Version = incomingVersion,
                        InstalledAt = DateTime.Now,
                        Source = source
                    };
                    _index.Plugins.Add(entry);
                    addedEntry = true;
                }
                else
                {
                    entry.Version = incomingVersion;
                    entry.Source = source;
                }

                var saveResult = JsonHelper.SaveToFile(LibraryIndexPath, _index);
                if (saveResult.IsFailure)
                {
                    throw new IOException(saveResult.Error?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            lock (_indexLock)
            {
                if (addedEntry && entry != null)
                {
                    _index.Plugins.Remove(entry);
                }
                else if (entry != null)
                {
                    entry.Version = previousVersion ?? entry.Version;
                    entry.Source = previousSource ?? entry.Source;
                }
            }

            try
            {
                if (stagingMovedToTarget &&
                    Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
                }

                if (targetMovedToBackup &&
                    !Directory.Exists(targetDirectory) &&
                    Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, targetDirectory);
                }

                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
            catch
            {
                // 保留原始异常；备份目录不会被主动删除，便于人工恢复。
            }

            return Result<InstalledPluginInfo>.Failure(
                Error.FileSystem(
                    PluginErrorCodes.InstallTransactionFailed,
                    $"插件安装事务失败: {ex.Message}",
                    ex,
                    filePath: targetDirectory));
        }

        var pluginInfo = InstalledPluginInfo.FromManifest(manifest, entry!.Source);
        pluginInfo.InstalledAt = entry.InstalledAt;

        try
        {
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
        }
        catch
        {
            // 新版本已经提交；旧版本备份可在后续维护时清理，不能因此回滚索引。
        }

        writeOperation.Dispose();
        OnPluginChanged(new PluginLibraryChangedEventArgs(
            isUpdate
                ? PluginLibraryChangeType.Updated
                : PluginLibraryChangeType.Installed,
            pluginId,
            pluginInfo));
        return Result<InstalledPluginInfo>.Success(pluginInfo);
    }

    private static void RestoreSavedFiles(
        string sourceRoot,
        string stagingRoot,
        IEnumerable<string> savedFiles)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (var relativePath in savedFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = GetContainedRelativePath(sourceRoot, relativePath);
            var destinationPath = GetContainedRelativePath(stagingRoot, relativePath);
            if (File.Exists(sourcePath))
            {
                DeletePathIfExists(destinationPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            else if (Directory.Exists(sourcePath))
            {
                DeletePathIfExists(destinationPath);
                CopyDirectory(sourcePath, destinationPath);
            }
        }
    }

    private static string GetContainedRelativePath(
        string rootDirectory,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') ||
            relativePath.Contains(':') ||
            relativePath.Split(
                    '/',
                    StringSplitOptions.None)
                .Any(
                    segment =>
                        string.IsNullOrWhiteSpace(segment) ||
                        segment == "." ||
                        segment == ".."))
        {
            throw new InvalidDataException(
                $"savedFiles 包含不安全路径: {relativePath}");
        }

        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"savedFiles 路径逃逸插件目录: {relativePath}");
        }

        return candidate;
    }

    private static void DeletePathIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void ExtractPluginPackage(string archivePath, string extractionRoot)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidDataException("插件包为空");
        }

        if (archive.Entries.Count > MaxPackageEntries)
        {
            throw new InvalidDataException($"插件包文件数量超过限制（{MaxPackageEntries}）");
        }

        long totalLength = 0;
        var rootPrefix = Path.GetFullPath(extractionRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > MaxPackageUncompressedBytes)
            {
                throw new InvalidDataException("插件包解压后大小超过 512 MiB");
            }

            var relativePath = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var pathSegments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (relativePath.StartsWith("/", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Contains(':') ||
                pathSegments.Any(segment => segment is "." or ".."))
            {
                throw new InvalidDataException($"插件包包含不安全路径: {entry.FullName}");
            }

            var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixFileType == 0xA000)
            {
                throw new InvalidDataException($"插件包不允许包含符号链接: {entry.FullName}");
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine(extractionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"插件包路径越界: {entry.FullName}");
            }

            if (relativePath.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }
    }

    private static string FindPluginRoot(string extractionRoot)
    {
        var manifestPaths = Directory
            .EnumerateFiles(extractionRoot, "plugin.json", SearchOption.AllDirectories)
            .ToArray();
        if (manifestPaths.Length != 1)
        {
            throw new InvalidDataException(
                manifestPaths.Length == 0
                    ? "插件包中没有 plugin.json"
                    : "插件包中存在多个 plugin.json");
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPaths[0])!;
        var relativeDirectory = Path.GetRelativePath(extractionRoot, manifestDirectory);
        if (relativeDirectory != "." &&
            relativeDirectory.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries).Length != 1)
        {
            throw new InvalidDataException("plugin.json 只能位于 ZIP 根目录或唯一的顶层插件目录中");
        }

        if (relativeDirectory != ".")
        {
            var topLevelEntries = Directory.EnumerateFileSystemEntries(extractionRoot).ToArray();
            if (topLevelEntries.Length != 1 ||
                !string.Equals(
                    Path.GetFullPath(topLevelEntries[0]),
                    Path.GetFullPath(manifestDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("顶层插件目录之外包含额外文件");
            }
        }

        return manifestDirectory;
    }

    private static void ValidatePackageEntryFiles(string pluginDirectory, PluginManifest manifest)
    {
        EnsurePackageFileExists(pluginDirectory, manifest.Main!, "插件入口文件");
        if (manifest.Companion != null)
        {
            EnsurePackageFileExists(pluginDirectory, manifest.Companion.Executable!, "companion 可执行文件");
        }
    }

    private static void EnsurePackageFileExists(string pluginDirectory, string relativePath, string displayName)
    {
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(normalized) ||
            normalized.Contains(':') ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"{displayName}路径不安全: {relativePath}");
        }

        var rootPrefix = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(
            Path.Combine(pluginDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new InvalidDataException($"{displayName}不存在: {relativePath}");
        }
    }

    private static void VerifyPackageManifestIfPresent(string pluginDirectory)
    {
        var manifestPath = Path.Combine(pluginDirectory, "package-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("files", out var filesElement) ||
            filesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("package-manifest.json 缺少 files 数组");
        }

        var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileElement in filesElement.EnumerateArray())
        {
            if (!fileElement.TryGetProperty("path", out var pathElement) ||
                !fileElement.TryGetProperty("size", out var sizeElement) ||
                !fileElement.TryGetProperty("sha256", out var hashElement))
            {
                throw new InvalidDataException("package-manifest.json 文件条目不完整");
            }

            var relativePath = pathElement.GetString() ?? string.Empty;
            EnsurePackageFileExists(pluginDirectory, relativePath, "清单文件");
            var normalizedPath = relativePath.Replace('\\', '/');
            if (!declaredPaths.Add(normalizedPath))
            {
                throw new InvalidDataException($"package-manifest.json 包含重复路径: {relativePath}");
            }

            var fullPath = Path.Combine(
                pluginDirectory,
                normalizedPath.Replace('/', Path.DirectorySeparatorChar));
            var expectedSize = sizeElement.GetInt64();
            var expectedHash = hashElement.GetString() ?? string.Empty;
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length != expectedSize)
            {
                throw new InvalidDataException($"插件包文件大小校验失败: {relativePath}");
            }

            using var stream = File.OpenRead(fullPath);
            var actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"插件包文件哈希校验失败: {relativePath}");
            }
        }

        var actualPaths = Directory
            .EnumerateFiles(pluginDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(pluginDirectory, path).Replace('\\', '/'))
            .Where(path => !string.Equals(path, "package-manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actualPaths.SetEquals(declaredPaths))
        {
            throw new InvalidDataException("插件包实际文件与 package-manifest.json 不一致");
        }
    }

    /// <summary>
    /// 复制目录及其内容
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        // 创建目标目录
        Directory.CreateDirectory(targetDir);

        // 复制文件
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        // 递归复制子目录
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(dir, targetSubDir);
        }
    }

#endregion

#region Uninstall Methods

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="force">是否强制卸载（忽略引用检查）</param>
    /// <param name="getReferencingProfiles">获取引用该插件的Profile列表的委托（用于解耦）</param>
    /// <returns>卸载结果</returns>
    public Result UninstallPlugin(string pluginId, bool force = false,
                                  Func<string, List<string>>? getReferencingProfiles = null)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result.Failure(
                Error.Plugin(PluginErrorCodes.InvalidManifest, "插件 ID 格式无效", pluginId: pluginId));
        }

        using var writeOperation = _writeCoordinator.Acquire();

        // 检查是否已安装
        if (!IsInstalled(pluginId))
        {
            return Result.Failure(
                Error.Plugin(PluginErrorCodes.NotInstalled, $"插件 {pluginId} 未安装", pluginId: pluginId));
        }

        // 检查引用（如果提供了委托且不是强制卸载）
        if (!force && getReferencingProfiles != null)
        {
            var profiles = getReferencingProfiles(pluginId);
            if (profiles.Count > 0)
            {
                var error = Error.Plugin(PluginErrorCodes.HasReferences,
                                         $"插件 {pluginId} 被 {profiles.Count} 个 Profile 引用", pluginId: pluginId);
                error.Metadata["ReferencingProfiles"] = profiles;
                return Result.Failure(error);
            }
        }

        // 仅删除用户插件库目录（内置 Repos 目录不应被删除）
        var pluginDir = GetContainedPluginDirectory(LibraryDirectory, pluginId);
        try
        {
            StopCompanionBeforeFileMutation(pluginId);
            if (!_permissionConsentService.RevokeHighRiskPermissionConsent(pluginId))
            {
                return Result.Failure(
                    Error.Plugin(
                        PluginErrorCodes.PermissionConsentDeclined,
                        "无法撤销插件的高风险权限授权，已取消卸载",
                        pluginId: pluginId));
            }

            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
            }
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.FileSystem(PluginErrorCodes.DeleteFailed, $"文件删除失败: {ex.Message}", ex,
                                                   filePath: pluginDir));
        }

        // 更新索引
        lock (_indexLock)
        {
            _index.Plugins.RemoveAll(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            SaveIndex();
        }

        writeOperation.Dispose();
        // 触发事件
        OnPluginChanged(new PluginLibraryChangedEventArgs(PluginLibraryChangeType.Uninstalled, pluginId));

        return Result.Success();
    }

#endregion

#region Events

    /// <summary>
    /// 插件库变化事件
    /// </summary>
    public event EventHandler<PluginLibraryChangedEventArgs>? PluginChanged;

    /// <summary>
    /// 触发插件变化事件
    /// </summary>
    protected virtual void OnPluginChanged(PluginLibraryChangedEventArgs e)
    {
        PluginChanged?.Invoke(this, e);
    }

#endregion

#region Update Methods

    /// <summary>
    /// 检查插件是否有可用更新
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>更新检查结果</returns>
    public UpdateCheckResult CheckForUpdate(string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return UpdateCheckResult.Invalid(pluginId, string.Empty, "插件 ID 格式无效");
        }

        string? currentVersion;
        lock (_indexLock)
        {
            currentVersion = _index.Plugins.FirstOrDefault(
                entry => string.Equals(entry.Id, pluginId, StringComparison.OrdinalIgnoreCase))?.Version;
        }

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return UpdateCheckResult.NoUpdate(pluginId, "未安装");
        }

        // 检查内置插件目录是否有该插件
        var builtInPath = GetContainedPluginDirectory(_builtInPluginsDirectory, pluginId);
        if (!Directory.Exists(builtInPath))
        {
            return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
        }

        // 读取内置插件的清单
        var manifestPath = Path.Combine(builtInPath, "plugin.json");
        var manifestResult = PluginManifest.LoadFromFile(manifestPath);
        if (!manifestResult.IsSuccess || manifestResult.Manifest == null)
        {
            return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
        }

        if (!string.Equals(manifestResult.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateCheckResult.Invalid(
                pluginId,
                currentVersion,
                $"更新清单中的 ID ({manifestResult.Manifest.Id}) 与插件 ID ({pluginId}) 不匹配");
        }

        var availableVersion = manifestResult.Manifest.Version ?? "1.0.0";

        // 比较版本号
        if (IsNewerVersion(currentVersion, availableVersion))
        {
            return UpdateCheckResult.WithUpdate(pluginId, currentVersion, availableVersion, builtInPath);
        }

        return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
    }

    /// <summary>
    /// 检查所有已安装插件的更新
    /// </summary>
    /// <returns>有更新的插件列表</returns>
    public List<UpdateCheckResult> CheckAllUpdates()
    {
        var results = new List<UpdateCheckResult>();
        var installedPlugins = GetInstalledPlugins();

        foreach (var plugin in installedPlugins)
        {
            var checkResult = CheckForUpdate(plugin.Id);
            if (checkResult.HasUpdate)
            {
                results.Add(checkResult);
            }
        }

        return results;
    }

    /// <summary>
    /// 更新插件到最新版本
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>更新结果</returns>
    public UpdateResult UpdatePlugin(string pluginId)
    {
        using var writeOperation = _writeCoordinator.Acquire();

        // 检查是否有可用更新
        var checkResult = CheckForUpdate(pluginId);
        if (!checkResult.HasUpdate)
        {
            return string.IsNullOrWhiteSpace(checkResult.ErrorMessage)
                ? UpdateResult.NoUpdateAvailable()
                : UpdateResult.Failed(checkResult.ErrorMessage);
        }

        var oldVersion = checkResult.CurrentVersion;
        var newVersion = checkResult.AvailableVersion!;
        var sourcePath = checkResult.SourcePath!;

        var incomingManifestResult = PluginManifest.LoadFromFile(Path.Combine(sourcePath, "plugin.json"));
        if (!incomingManifestResult.IsSuccess || incomingManifestResult.Manifest == null)
        {
            return UpdateResult.Failed($"更新清单无效: {incomingManifestResult.ErrorMessage}");
        }

        if (!string.Equals(incomingManifestResult.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateResult.Failed(
                $"更新清单中的 ID ({incomingManifestResult.Manifest.Id}) 与插件 ID ({pluginId}) 不匹配");
        }

        if (!_permissionConsentService.EnsureHighRiskPermissionsApproved(
                incomingManifestResult.Manifest,
                PluginPermissionConsentOperation.Update))
        {
            return UpdateResult.Failed("未获得或无法保存更新所需的高风险权限授权");
        }

        // 获取目标目录（仅更新用户插件库目录）
        var targetDir = GetContainedPluginDirectory(LibraryDirectory, pluginId);

        try
        {
            StopCompanionBeforeFileMutation(pluginId);

            // 删除旧文件（保留配置目录 - 配置在 Profile 目录中，不在这里）
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }

            // 复制新文件
            CopyDirectory(sourcePath, targetDir);

            // 更新索引中的版本号
            lock (_indexLock)
            {
                var entry = _index.Plugins.FirstOrDefault(
                    p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    entry.Version = newVersion;
                    SaveIndex();
                }
            }

            // 获取更新后的插件信息
            var pluginInfo = GetInstalledPluginInfo(pluginId);

            writeOperation.Dispose();
            // 触发 PluginChanged 事件 (Updated)
            OnPluginChanged(new PluginLibraryChangedEventArgs(PluginLibraryChangeType.Updated, pluginId, pluginInfo));

            return UpdateResult.Success(oldVersion, newVersion);
        }
        catch (Exception ex)
        {
            return UpdateResult.Failed($"更新失败: {ex.Message}");
        }
    }

#endregion

#region Version Comparison

    /// <summary>
    /// 比较两个语义化版本号
    /// </summary>
    /// <param name="version1">第一个版本号</param>
    /// <param name="version2">第二个版本号</param>
    /// <returns>
    /// 负数: version1 &lt; version2
    /// 零: version1 == version2
    /// 正数: version1 &gt; version2
    /// </returns>
    public static int CompareVersions(string? version1, string? version2)
    {
        if (string.IsNullOrWhiteSpace(version1) && string.IsNullOrWhiteSpace(version2))
            return 0;
        if (string.IsNullOrWhiteSpace(version1))
            return -1;
        if (string.IsNullOrWhiteSpace(version2))
            return 1;

        if (!TryParseSemanticVersion(version1, out var parsed1) ||
            !TryParseSemanticVersion(version2, out var parsed2))
        {
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
        }

        var coreComparison = parsed1.Major.CompareTo(parsed2.Major);
        if (coreComparison != 0)
            return coreComparison;

        coreComparison = parsed1.Minor.CompareTo(parsed2.Minor);
        if (coreComparison != 0)
            return coreComparison;

        coreComparison = parsed1.Patch.CompareTo(parsed2.Patch);
        if (coreComparison != 0)
            return coreComparison;

        if (parsed1.PreRelease.Length == 0)
            return parsed2.PreRelease.Length == 0 ? 0 : 1;
        if (parsed2.PreRelease.Length == 0)
            return -1;

        var identifierCount = Math.Min(parsed1.PreRelease.Length, parsed2.PreRelease.Length);
        for (var index = 0; index < identifierCount; index++)
        {
            var identifierComparison = ComparePreReleaseIdentifiers(
                parsed1.PreRelease[index],
                parsed2.PreRelease[index]);
            if (identifierComparison != 0)
                return identifierComparison;
        }

        return parsed1.PreRelease.Length.CompareTo(parsed2.PreRelease.Length);
    }

    private static bool TryParseSemanticVersion(string value, out SemanticPluginVersion version)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var buildMetadataIndex = normalized.IndexOf('+');
        if (buildMetadataIndex >= 0)
        {
            normalized = normalized[..buildMetadataIndex];
        }

        var preReleaseIndex = normalized.IndexOf('-');
        var core = preReleaseIndex >= 0 ? normalized[..preReleaseIndex] : normalized;
        var preRelease = preReleaseIndex >= 0
            ? normalized[(preReleaseIndex + 1)..].Split('.')
            : [];
        var coreParts = core.Split('.');
        var minor = 0L;
        var patch = 0L;

        if (coreParts.Length is < 1 or > 3 ||
            preRelease.Any(string.IsNullOrEmpty) ||
            !long.TryParse(coreParts[0], out var major) ||
            (coreParts.Length > 1 && !long.TryParse(coreParts[1], out minor)) ||
            (coreParts.Length > 2 && !long.TryParse(coreParts[2], out patch)))
        {
            version = default;
            return false;
        }

        version = new SemanticPluginVersion(
            major,
            minor,
            patch,
            preRelease);
        return true;
    }

    private static int ComparePreReleaseIdentifiers(string left, string right)
    {
        var leftIsNumeric = left.All(char.IsDigit);
        var rightIsNumeric = right.All(char.IsDigit);

        if (leftIsNumeric && rightIsNumeric)
        {
            var normalizedLeft = left.TrimStart('0');
            var normalizedRight = right.TrimStart('0');
            normalizedLeft = normalizedLeft.Length == 0 ? "0" : normalizedLeft;
            normalizedRight = normalizedRight.Length == 0 ? "0" : normalizedRight;

            var lengthComparison = normalizedLeft.Length.CompareTo(normalizedRight.Length);
            return lengthComparison != 0
                ? lengthComparison
                : string.Compare(normalizedLeft, normalizedRight, StringComparison.Ordinal);
        }

        if (leftIsNumeric != rightIsNumeric)
            return leftIsNumeric ? -1 : 1;

        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private readonly record struct SemanticPluginVersion(
        long Major,
        long Minor,
        long Patch,
        string[] PreRelease);

    /// <summary>
    /// 检查 availableVersion 是否比 currentVersion 更新
    /// </summary>
    /// <param name="currentVersion">当前版本</param>
    /// <param name="availableVersion">可用版本</param>
    /// <returns>如果可用版本更新则返回 true</returns>
    public static bool IsNewerVersion(string? currentVersion, string? availableVersion)
    {
        return CompareVersions(availableVersion, currentVersion) > 0;
    }

    private void StopCompanionBeforeFileMutation(string pluginId)
    {
        _companionProcessManager.StopAsync(pluginId).GetAwaiter().GetResult();
    }

    private static string GetContainedPluginDirectory(string rootDirectory, string pluginId)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            throw new ArgumentException("Plugin ID format is invalid.", nameof(pluginId));
        }

        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, pluginId));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Plugin directory escapes its allowed root.");
        }

        return candidate;
    }

#endregion

#region Utility Methods

    /// <summary>
    /// 获取可用的内置插件（未安装的）
    /// </summary>
    public List<PluginManifest> GetAvailableBuiltInPlugins()
    {
        var result = new List<PluginManifest>();

        if (!Directory.Exists(_builtInPluginsDirectory))
            return result;

        foreach (var dir in Directory.GetDirectories(_builtInPluginsDirectory))
        {
            var pluginId = Path.GetFileName(dir);

            // 跳过已安装的
            if (IsInstalled(pluginId))
                continue;

            var manifestPath = Path.Combine(dir, "plugin.json");
            var manifestResult = PluginManifest.LoadFromFile(manifestPath);
            if (manifestResult.IsSuccess && manifestResult.Manifest != null)
            {
                result.Add(manifestResult.Manifest);
            }
        }

        return result;
    }

#endregion
}
}
