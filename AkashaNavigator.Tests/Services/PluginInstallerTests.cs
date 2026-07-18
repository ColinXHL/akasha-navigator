using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginInstallerTests : IDisposable
{
    private const string PluginId = "sample-plugin";

    private readonly string _repositoryDirectory =
        Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginInstallerTests.{Guid.NewGuid():N}");

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_AdaptsManifestAndSubscribes()
    {
        var entry = CreateEntry();
        WriteCatalogPlugin(CreateManifest());
        var repository = CreateRepositoryService(entry);
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                entry))
            .Returns(
                Result<PluginSubscriptionRecord>.Success(
                    new PluginSubscriptionRecord { PluginId = PluginId }));
        subscriptions
            .Setup(service => service.MarkInstalled(
                PluginId,
                "1.0.0",
                new string('b', 40)))
            .Returns(Result.Success());
        PluginManifest? capturedManifest = null;
        IReadOnlyList<string>? capturedSavedFiles = null;
        string? capturedSource = null;
        var library = new Mock<IPluginLibrary>();
        library
            .Setup(service => service.InstallOrUpdateFromDirectory(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>()))
            .Callback<string, IReadOnlyList<string>, string>(
                (directory, savedFiles, source) =>
                {
                    capturedManifest = PluginManifest
                        .LoadFromFile(
                            Path.Combine(
                                directory,
                                AppConstants.PluginManifestFileName))
                        .Manifest;
                    capturedSavedFiles = savedFiles.ToArray();
                    capturedSource = source;
                })
            .Returns(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = PluginId,
                        Name = "Sample",
                        Version = "1.0.0"
                    }));
        var installer = new PluginInstaller(
            repository.Object,
            subscriptions.Object,
            library.Object,
            () => "1.4.0");

        var result = installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PluginId, capturedManifest!.Id);
        Assert.Equal("Colin", capturedManifest.Author);
        Assert.Equal("1.4.0", capturedManifest.MinAppVersion);
        Assert.Equal(new[] { "config.json" }, capturedSavedFiles);
        Assert.Equal(AppConstants.PluginInstallSourceRepository, capturedSource);
        subscriptions.Verify(
            service => service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                entry),
            Times.Once);
        subscriptions.Verify(
            service => service.MarkInstalled(
                PluginId,
                "1.0.0",
                new string('b', 40)),
            Times.Once);
    }

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_RejectsSavedEntryScript()
    {
        var entry = CreateEntry();
        var manifest = CreateManifest();
        manifest.SavedFiles = new List<string> { "main.js" };
        WriteCatalogPlugin(manifest);
        var subscriptions = new Mock<IPluginSubscriptionService>();
        var library = new Mock<IPluginLibrary>();
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions.Object,
            library.Object,
            () => "1.4.0");

        var result = installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(
            PluginErrorCodes.RepositoryManifestInvalid,
            result.Error!.Code);
        subscriptions.Verify(
            service => service.Subscribe(
                It.IsAny<string>(),
                It.IsAny<PluginRepositoryEntry>()),
            Times.Never);
        library.Verify(
            service => service.InstallOrUpdateFromDirectory(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_RejectsUnsupportedDistribution()
    {
        var entry = CreateEntry();
        entry.DistributionType = AppConstants.PluginDistributionRelease;
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            Mock.Of<IPluginSubscriptionService>(),
            Mock.Of<IPluginLibrary>(),
            () => "1.4.0");

        var result = installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.DistributionUnsupported, result.Error!.Code);
    }

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_RejectsIncompatibleHost()
    {
        var entry = CreateEntry();
        WriteCatalogPlugin(CreateManifest());
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            Mock.Of<IPluginSubscriptionService>(),
            Mock.Of<IPluginLibrary>(),
            () => "1.3.9");

        var result = installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.HostVersionTooLow, result.Error!.Code);
    }

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_EndToEndPreservesUserFile()
    {
        var entry = CreateEntry();
        var manifest = CreateManifest();
        WriteCatalogPlugin(manifest);
        var installRoot = Path.Combine(_repositoryDirectory, "test-install");
        var library = new PluginLibrary(
            Path.Combine(installRoot, "installed"),
            Path.Combine(installRoot, "library.json"),
            Path.Combine(installRoot, "builtin"),
            Mock.Of<ICompanionProcessManager>(),
            CreateConsentService());
        var subscriptions = new PluginSubscriptionService(
            Mock.Of<ILogService>(),
            Path.Combine(installRoot, "plugin-subscriptions.json"));
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions,
            library,
            () => "1.4.0");

        var installResult =
            installer.InstallOrUpdateRepositoryPlugin(PluginId);
        Assert.True(installResult.IsSuccess, installResult.Error?.Message);
        var installedDirectory = Path.Combine(
            installRoot,
            "installed",
            PluginId);
        File.WriteAllText(
            Path.Combine(installedDirectory, "config.json"),
            "user config");

        entry.Version = "2.0.0";
        manifest.Version = "2.0.0";
        WriteCatalogPlugin(manifest);
        var updateResult =
            installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(updateResult.IsSuccess, updateResult.Error?.Message);
        Assert.Equal(
            "user config",
            File.ReadAllText(
                Path.Combine(installedDirectory, "config.json")));
        Assert.Equal("2.0.0", library.GetInstalledPluginInfo(PluginId)?.Version);
        var subscription = subscriptions.GetSubscription(PluginId);
        Assert.Equal("2.0.0", subscription?.InstalledVersion);
        Assert.Equal(new string('b', 40), subscription?.InstalledCommit);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repositoryDirectory))
            {
                Directory.Delete(_repositoryDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private Mock<IPluginRepositoryService> CreateRepositoryService(
        PluginRepositoryEntry entry)
    {
        var repository = new Mock<IPluginRepositoryService>();
        repository
            .SetupGet(service => service.RepositoryDirectory)
            .Returns(_repositoryDirectory);
        repository
            .SetupGet(service => service.Settings)
            .Returns(new PluginRepositorySettings());
        repository
            .SetupGet(service => service.Current)
            .Returns(
                new PluginRepositorySnapshot(
                    new PluginRepositoryIndex {
                        SchemaVersion = 1,
                        Commit = new string('a', 40),
                        Plugins = new List<PluginRepositoryEntry> { entry }
                    },
                    new string('b', 40),
                    false));
        return repository;
    }

    private void WriteCatalogPlugin(CatalogPluginManifest manifest)
    {
        var pluginDirectory = Path.Combine(
            _repositoryDirectory,
            "plugins",
            PluginId);
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.js"), "main");
        File.WriteAllText(Path.Combine(pluginDirectory, "config.json"), "default");
        var result = JsonHelper.SaveToFile(
            Path.Combine(
                pluginDirectory,
                AppConstants.PluginRepositoryManifestFileName),
            manifest);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static PluginRepositoryEntry CreateEntry()
    {
        return new PluginRepositoryEntry {
            Id = PluginId,
            Path = $"plugins/{PluginId}",
            Name = "Sample",
            Version = "1.0.0",
            Description = "Sample plugin",
            DistributionType = AppConstants.PluginDistributionRepository,
            MinHostVersion = "1.4.0"
        };
    }

    private static CatalogPluginManifest CreateManifest()
    {
        return new CatalogPluginManifest {
            ManifestVersion = 2,
            Id = PluginId,
            Name = "Sample",
            Version = "1.0.0",
            Description = "Sample plugin",
            Authors = new List<CatalogPluginAuthor> {
                new() { Name = "Colin" }
            },
            Host = new CatalogPluginHost { MinVersion = "1.4.0" },
            Main = "main.js",
            Permissions = new List<string>(),
            SavedFiles = new List<string> { "config.json" },
            Library = new List<string>(),
            HttpAllowedUrls = new List<string>(),
            Distribution = new CatalogPluginDistribution {
                Type = AppConstants.PluginDistributionRepository
            }
        };
    }

    private static IPluginPermissionConsentService CreateConsentService()
    {
        var consent = new Mock<IPluginPermissionConsentService>();
        consent
            .Setup(service => service.EnsureHighRiskPermissionsApproved(
                It.IsAny<PluginManifest>(),
                It.IsAny<PluginPermissionConsentOperation>()))
            .Returns(true);
        return consent.Object;
    }
}
