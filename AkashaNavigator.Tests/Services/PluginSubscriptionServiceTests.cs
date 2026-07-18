using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginSubscriptionServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginSubscriptionTests.{Guid.NewGuid():N}");
    private readonly Mock<ILogService> _logService = new();

    [Fact]
    public void Subscribe_PersistsRepositoryPathAndReloads()
    {
        var statePath = GetStatePath();
        var service = new PluginSubscriptionService(_logService.Object, statePath);

        var result = service.Subscribe("official", CreateEntry());
        var reloaded = new PluginSubscriptionService(_logService.Object, statePath);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var record = Assert.Single(reloaded.GetSubscriptions());
        Assert.Equal("sample-plugin", record.PluginId);
        Assert.Equal("official", record.RepositoryId);
        Assert.Equal("plugins/sample-plugin", record.RepositoryPath);
        Assert.True(record.AutoUpdate);
        Assert.True(record.IsAvailable);
    }

    [Fact]
    public void Subscribe_RejectsSameIdFromDifferentRepository()
    {
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath());
        Assert.True(service.Subscribe("official", CreateEntry()).IsSuccess);

        var result = service.Subscribe("custom", CreateEntry());

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PLUGIN_SUBSCRIPTION_SOURCE_CONFLICT",
            result.Error!.Code);
    }

    [Fact]
    public void Reconcile_MarksRemovedPluginButKeepsSubscription()
    {
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath());
        Assert.True(service.Subscribe("official", CreateEntry()).IsSuccess);
        var snapshot = new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = new string('a', 40),
                Plugins = new List<PluginRepositoryEntry>()
            },
            new string('b', 40),
            false);

        var result = service.Reconcile("official", snapshot);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(service.GetSubscriptions());
        Assert.False(record.IsAvailable);
        Assert.Equal("1.0.0", record.LastKnownVersion);
    }

    [Fact]
    public void Unsubscribe_RemovesOnlySubscriptionState()
    {
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath());
        Assert.True(service.Subscribe("official", CreateEntry()).IsSuccess);
        var installedMarker = Path.Combine(_root, "installed", "user-data.json");
        Directory.CreateDirectory(Path.GetDirectoryName(installedMarker)!);
        File.WriteAllText(installedMarker, "keep");

        var result = service.Unsubscribe("sample-plugin");

        Assert.True(result.IsSuccess);
        Assert.Empty(service.GetSubscriptions());
        Assert.True(File.Exists(installedMarker));
    }

    [Fact]
    public void Reconcile_ReportsInstalledSubscriptionUpdate()
    {
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath());
        Assert.True(service.Subscribe("official", CreateEntry()).IsSuccess);
        Assert.True(
            service.MarkInstalled(
                "sample-plugin",
                "1.0.0",
                new string('a', 40)).IsSuccess);
        var entry = CreateEntry();
        entry.Version = "2.0.0";
        var snapshot = new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = new string('b', 40),
                Plugins = new List<PluginRepositoryEntry> { entry }
            },
            new string('c', 40),
            false);

        Assert.True(service.Reconcile("official", snapshot).IsSuccess);
        var update = Assert.Single(service.GetAvailableUpdates());

        Assert.Equal("sample-plugin", update.PluginId);
        Assert.Equal("1.0.0", update.InstalledVersion);
        Assert.Equal("2.0.0", update.AvailableVersion);
        Assert.True(update.AutoUpdate);
    }

    [Fact]
    public void SetAutoUpdate_PersistsPerPluginPreference()
    {
        var statePath = GetStatePath();
        var service = new PluginSubscriptionService(_logService.Object, statePath);
        Assert.True(service.Subscribe("official", CreateEntry()).IsSuccess);

        var result = service.SetAutoUpdate("sample-plugin", enabled: false);
        var reloaded = new PluginSubscriptionService(_logService.Object, statePath);

        Assert.True(result.IsSuccess);
        Assert.False(
            Assert.Single(reloaded.GetSubscriptions()).AutoUpdate);
    }

    [Theory]
    [InlineData(AppConstants.PluginInstallSourceBuiltIn)]
    [InlineData(AppConstants.PluginInstallSourceMigrated)]
    public void Reconcile_AdoptsLegacyInstallationWithoutEnablingAutoUpdate(
        string legacySource)
    {
        var installedPlugin = new InstalledPluginInfo {
            Id = "sample-plugin",
            Name = "Sample",
            Version = "0.9.0",
            Source = legacySource
        };
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(new List<InstalledPluginInfo> { installedPlugin });
        var statePath = GetStatePath();
        var service = new PluginSubscriptionService(
            _logService.Object,
            statePath,
            pluginLibrary.Object);
        var snapshot = CreateSnapshot(CreateEntry());

        var result = service.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            snapshot);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var record = Assert.Single(service.GetSubscriptions());
        Assert.Equal("sample-plugin", record.PluginId);
        Assert.Equal("0.9.0", record.InstalledVersion);
        Assert.Equal(snapshot.CatalogCommit, record.InstalledCommit);
        Assert.False(record.AutoUpdate);
        Assert.True(record.IsAvailable);

        var reloaded = new PluginSubscriptionService(
            _logService.Object,
            statePath);
        Assert.False(Assert.Single(reloaded.GetSubscriptions()).AutoUpdate);
    }

    [Fact]
    public void Reconcile_AdoptsLegacyNoticeAutomationInstallation()
    {
        var installedPlugin = new InstalledPluginInfo {
            Id = AppConstants.AutomationPluginId,
            Name = "Akasha Genshin Automation",
            Version = "0.4.2",
            Source = AppConstants.PluginInstallSourceExternal
        };
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(new List<InstalledPluginInfo> { installedPlugin });
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath(),
            pluginLibrary.Object);
        var entry = CreateEntry();
        entry.Id = AppConstants.AutomationPluginId;
        entry.Path = $"plugins/{AppConstants.AutomationPluginId}";
        entry.Name = installedPlugin.Name;
        entry.Version = "0.4.3";
        entry.DistributionType = AppConstants.PluginDistributionRelease;
        entry.HasBackend = true;
        var snapshot = CreateSnapshot(entry);

        var result = service.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            snapshot);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var record = Assert.Single(service.GetSubscriptions());
        Assert.Equal(AppConstants.AutomationPluginId, record.PluginId);
        Assert.Equal(installedPlugin.Version, record.InstalledVersion);
        Assert.Equal(snapshot.CatalogCommit, record.InstalledCommit);
        Assert.False(record.AutoUpdate);
        Assert.True(record.IsAvailable);
    }

    [Fact]
    public void Reconcile_DoesNotAdoptExternalOrUnknownLegacyInstallation()
    {
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "1.0.0",
                        Source = AppConstants.PluginInstallSourceExternal
                    },
                    new() {
                        Id = "unknown-plugin",
                        Name = "Unknown",
                        Version = "1.0.0",
                        Source = AppConstants.PluginInstallSourceBuiltIn
                    }
                });
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath(),
            pluginLibrary.Object);

        var result = service.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            CreateSnapshot(CreateEntry()));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(service.GetSubscriptions());
    }

    [Fact]
    public void Reconcile_DoesNotOverwriteExistingSubscriptionPreference()
    {
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "1.0.0",
                        Source = AppConstants.PluginInstallSourceBuiltIn
                    }
                });
        var service = new PluginSubscriptionService(
            _logService.Object,
            GetStatePath(),
            pluginLibrary.Object);
        Assert.True(
            service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                CreateEntry()).IsSuccess);

        var result = service.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            CreateSnapshot(CreateEntry()));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(Assert.Single(service.GetSubscriptions()).AutoUpdate);
    }

    [Fact]
    public void Reconcile_RollsBackLegacyAdoptionWhenStateCannotBeSaved()
    {
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "1.0.0",
                        Source = AppConstants.PluginInstallSourceBuiltIn
                    }
                });
        var statePath = GetStatePath();
        Directory.CreateDirectory(statePath);
        var service = new PluginSubscriptionService(
            _logService.Object,
            statePath,
            pluginLibrary.Object);

        var result = service.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            CreateSnapshot(CreateEntry()));

        Assert.True(result.IsFailure);
        Assert.Empty(service.GetSubscriptions());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string GetStatePath()
    {
        return Path.Combine(_root, "plugin-subscriptions.json");
    }

    private static PluginRepositoryEntry CreateEntry()
    {
        return new PluginRepositoryEntry {
            Id = "sample-plugin",
            Path = "plugins/sample-plugin",
            Name = "Sample",
            Version = "1.0.0",
            Description = "Sample plugin",
            DistributionType = AppConstants.PluginDistributionRepository,
            MinHostVersion = "1.4.0"
        };
    }

    private static PluginRepositorySnapshot CreateSnapshot(
        params PluginRepositoryEntry[] entries)
    {
        return new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = new string('a', 40),
                Plugins = entries.ToList()
            },
            new string('b', 40),
            false);
    }
}
