using System.IO;

namespace AkashaNavigator.Models.PluginRepository;

public sealed class ResolvedPluginDistribution : IDisposable
{
    private readonly string _cleanupDirectory;

    public ResolvedPluginDistribution(
        string sourceDirectory,
        string? cleanupDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        SourceDirectory = Path.GetFullPath(sourceDirectory);
        _cleanupDirectory = Path.GetFullPath(
            cleanupDirectory ?? sourceDirectory);
    }

    public string SourceDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_cleanupDirectory))
            {
                Directory.Delete(_cleanupDirectory, recursive: true);
            }
        }
        catch
        {
            // staging 目录由系统后续清理。
        }
    }
}
