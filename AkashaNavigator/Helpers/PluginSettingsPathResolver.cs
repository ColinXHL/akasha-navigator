using System;
using System.IO;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Helpers;

internal static class PluginSettingsPathResolver
{
    public static string? ResolveDirectory(string pluginDirectory, string? relativePath)
    {
        return ResolvePath(pluginDirectory, relativePath);
    }

    public static string? ResolveSettingsFile(string pluginDirectory, string? manifestSettingsPath)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        var relativePath = manifestSettingsPath;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            var repositoryManifestResult =
                JsonHelper.LoadFromFile<CatalogPluginManifest>(
                    Path.Combine(
                        pluginDirectory,
                        AppConstants.PluginRepositoryManifestFileName));
            relativePath = repositoryManifestResult.IsSuccess
                ? repositoryManifestResult.Value?.Settings
                : null;
        }

        relativePath = string.IsNullOrWhiteSpace(relativePath)
            ? AppConstants.PluginSettingsUiFileName
            : relativePath;
        return ResolvePath(pluginDirectory, relativePath);
    }

    private static string? ResolvePath(string pluginDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return null;
        }

        var root = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return root;
        }

        var target = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        return target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? target : null;
    }
}
