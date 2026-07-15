using System.IO;

namespace AkashaNavigator.Services.Companion;

internal static class CompanionPathValidator
{
    public static string ResolveExecutable(string pluginDirectory, string relativeExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeExecutable);

        if (Path.IsPathRooted(relativeExecutable) || relativeExecutable.Contains(':'))
        {
            throw new InvalidDataException("Companion executable must be a relative path.");
        }

        var root = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = relativeExecutable.Replace('/', Path.DirectorySeparatorChar);
        var executable = Path.GetFullPath(Path.Combine(root, relative));
        var rootPrefix = root + Path.DirectorySeparatorChar;

        if (!executable.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Companion executable escapes the plugin directory.");
        }

        if (!string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Companion executable must be an EXE file.");
        }

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Companion executable was not found.", executable);
        }

        AssertNoReparsePoints(root, executable);
        return executable;
    }

    private static void AssertNoReparsePoints(string root, string executable)
    {
        var current = root;
        AssertNotReparsePoint(current);

        var relative = Path.GetRelativePath(root, executable);
        foreach (var segment in relative.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            AssertNotReparsePoint(current);
        }
    }

    private static void AssertNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Companion executable path cannot contain reparse points or links.");
        }
    }
}
