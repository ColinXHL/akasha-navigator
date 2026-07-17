using System;
using System.IO;

namespace AkashaNavigator.Helpers;

internal static class PluginSettingsPathResolver
{
    public static string? ResolveDirectory(string pluginDirectory, string? relativePath)
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
