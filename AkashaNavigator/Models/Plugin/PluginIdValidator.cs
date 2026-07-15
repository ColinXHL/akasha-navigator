namespace AkashaNavigator.Models.Plugin;

/// <summary>
/// Validates plugin identifiers before they are used as directory names or identity keys.
/// </summary>
public static class PluginIdValidator
{
    public const int MaximumLength = 128;

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool IsValid(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || pluginId.Length > MaximumLength ||
            !IsAsciiLetterOrDigit(pluginId[0]) || pluginId[^1] == '.')
        {
            return false;
        }

        foreach (var character in pluginId)
        {
            if (!IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        var deviceName = pluginId.Split('.')[0];
        return !ReservedWindowsNames.Contains(deviceName);
    }

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
