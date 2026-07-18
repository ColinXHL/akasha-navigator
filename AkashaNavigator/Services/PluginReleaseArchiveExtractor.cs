using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AkashaNavigator.Services;

/// <summary>
/// 将已完成外层大小和哈希校验的 Release ZIP 安全解压到 staging。
/// </summary>
internal static class PluginReleaseArchiveExtractor
{
    private const int MaxPackageEntries = 20_000;
    private const long MaxPackageEntryBytes = 256L * 1024 * 1024;
    private const long MaxPackageUncompressedBytes = 512L * 1024 * 1024;

    public static string Extract(string archivePath, string extractionRoot)
    {
        Directory.CreateDirectory(extractionRoot);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidDataException("插件包为空");
        }

        if (archive.Entries.Count > MaxPackageEntries)
        {
            throw new InvalidDataException(
                $"插件包文件数量超过限制（{MaxPackageEntries}）");
        }

        long totalLength = 0;
        var rootPrefix = Path.GetFullPath(extractionRoot)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxPackageEntryBytes)
            {
                throw new InvalidDataException(
                    $"插件包单个文件超过 256 MiB: {entry.FullName}");
            }

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

            var pathSegments = relativePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);
            if (relativePath.StartsWith("/", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Contains(':') ||
                pathSegments.Any(segment => segment is "." or ".."))
            {
                throw new InvalidDataException(
                    $"插件包包含不安全路径: {entry.FullName}");
            }

            var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            var windowsAttributes = entry.ExternalAttributes & 0xFFFF;
            if (unixFileType == 0xA000 ||
                (windowsAttributes &
                 (int)FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"插件包不允许包含符号链接或重解析点: {entry.FullName}");
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine(
                    extractionRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(
                    rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"插件包路径越界: {entry.FullName}");
            }

            if (relativePath.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            source.CopyTo(destination);
        }

        return FindPluginRoot(extractionRoot);
    }

    private static string FindPluginRoot(string extractionRoot)
    {
        var manifestPaths = Directory
            .EnumerateFiles(
                extractionRoot,
                AppConstants.PluginRepositoryManifestFileName,
                SearchOption.AllDirectories)
            .ToArray();
        if (manifestPaths.Length != 1)
        {
            throw new InvalidDataException(
                manifestPaths.Length == 0
                    ? $"插件包中没有 {AppConstants.PluginRepositoryManifestFileName}"
                    : $"插件包中存在多个 {AppConstants.PluginRepositoryManifestFileName}");
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPaths[0])!;
        var relativeDirectory =
            Path.GetRelativePath(extractionRoot, manifestDirectory);
        if (relativeDirectory != "." &&
            relativeDirectory.Split(
                new[] {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries).Length != 1)
        {
            throw new InvalidDataException(
                $"{AppConstants.PluginRepositoryManifestFileName} 只能位于 ZIP 根目录或唯一的顶层插件目录中");
        }

        if (relativeDirectory != ".")
        {
            var topLevelEntries =
                Directory.EnumerateFileSystemEntries(extractionRoot).ToArray();
            if (topLevelEntries.Length != 1 ||
                !string.Equals(
                    Path.GetFullPath(topLevelEntries[0]),
                    Path.GetFullPath(manifestDirectory),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "顶层插件目录之外包含额外文件");
            }
        }

        return manifestDirectory;
    }
}
