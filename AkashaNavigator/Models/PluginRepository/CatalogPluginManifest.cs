using System.Text.Json;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Models.PluginRepository;

/// <summary>
/// Akasha 插件仓库 Manifest v2。
/// </summary>
public sealed class CatalogPluginManifest
{
    public int ManifestVersion { get; set; }

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<CatalogPluginAuthor> Authors { get; set; } = new();

    public string? Homepage { get; set; }

    public CatalogPluginHost Host { get; set; } = new();

    public string Main { get; set; } = string.Empty;

    public string? Settings { get; set; }

    public List<string> Permissions { get; set; } = new();

    public List<string> Profiles { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public List<string> SavedFiles { get; set; } = new();

    public List<string> Library { get; set; } = new();

    public List<string> HttpAllowedUrls { get; set; } = new();

    public Dictionary<string, JsonElement> DefaultConfig { get; set; } = new();

    public CatalogPluginDistribution Distribution { get; set; } = new();

    public CatalogPluginBackend? Backend { get; set; }

    public PluginManifest ToRuntimeManifest()
    {
        return new PluginManifest {
            Id = Id,
            Name = Name,
            Version = Version,
            Main = Main,
            Description = Description,
            Author = Authors?.FirstOrDefault()?.Name,
            MinAppVersion = Host?.MinVersion,
            Settings = Settings,
            Permissions = Permissions?.ToList() ?? new List<string>(),
            Companion = Backend == null
                ? null
                : new CompanionManifest {
                    Executable = Backend.Entry,
                    ProtocolVersion = Backend.ProtocolVersion,
                    Lifetime = Backend.Lifetime,
                    SingleInstance = true,
                    ShutdownTimeoutMs = Backend.ShutdownTimeoutMs
                },
            DefaultConfig = DefaultConfig == null
                ? new Dictionary<string, JsonElement>()
                : new Dictionary<string, JsonElement>(DefaultConfig),
            Library = Library?.ToList() ?? new List<string>(),
            HttpAllowedUrls = HttpAllowedUrls?.ToList() ?? new List<string>()
        };
    }
}

public sealed class CatalogPluginAuthor
{
    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }
}

public sealed class CatalogPluginHost
{
    public string MinVersion { get; set; } = string.Empty;
}

public sealed class CatalogPluginDistribution
{
    public string Type { get; set; } = string.Empty;

    public string? Tag { get; set; }

    public string? Asset { get; set; }

    public string? Sha256 { get; set; }

    public long? Size { get; set; }
}

public sealed class CatalogPluginBackend
{
    public string Type { get; set; } = string.Empty;

    public string Entry { get; set; } = string.Empty;

    public int ProtocolVersion { get; set; }

    public string Lifetime { get; set; } = string.Empty;

    public string IntegrityLevel { get; set; } = string.Empty;

    public int ShutdownTimeoutMs { get; set; }
}
