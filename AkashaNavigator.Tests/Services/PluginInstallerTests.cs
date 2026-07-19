using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;
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
    public void InstallOrUpdateRepositoryPlugin_RejectsReleaseWhenDownloaderIsUnavailable()
    {
        var entry = CreateEntry();
        entry.DistributionType = AppConstants.PluginDistributionRelease;
        var manifest = CreateManifest();
        manifest.Distribution = CreateReleaseDistribution();
        WriteCatalogPlugin(manifest);
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                entry))
            .Returns(
                Result<PluginSubscriptionRecord>.Success(
                    new PluginSubscriptionRecord { PluginId = PluginId }));
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions.Object,
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
    public void InstallOrUpdateRepositoryPlugin_AllowsPreviewHostOnSameReleaseLine()
    {
        var entry = CreateEntry();
        WriteCatalogPlugin(CreateManifest());
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
        var library = new Mock<IPluginLibrary>();
        library
            .Setup(service => service.InstallOrUpdateFromDirectory(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>()))
            .Returns(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = PluginId,
                        Name = "Sample",
                        Version = "1.0.0"
                    }));
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions.Object,
            library.Object,
            () => "1.4.0-alpha.2");

        var result = installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(result.IsSuccess, result.Error?.Message);
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

    [Fact]
    public void InstallOrUpdateRepositoryPlugin_EndToEndPreservesDirectoryMarker()
    {
        var entry = CreateEntry();
        var manifest = CreateManifest();
        manifest.SavedFiles = new List<string> { "data/" };
        WriteCatalogPlugin(manifest);
        var installRoot = Path.Combine(
            _repositoryDirectory,
            "directory-marker-install");
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
            () => "1.4.0-alpha.2");

        var installResult =
            installer.InstallOrUpdateRepositoryPlugin(PluginId);
        Assert.True(installResult.IsSuccess, installResult.Error?.Message);
        var installedDirectory = Path.Combine(
            installRoot,
            "installed",
            PluginId);
        var userDataDirectory = Path.Combine(installedDirectory, "data");
        Directory.CreateDirectory(userDataDirectory);
        File.WriteAllText(
            Path.Combine(userDataDirectory, "user.json"),
            "user data");

        entry.Version = "2.0.0";
        manifest.Version = "2.0.0";
        WriteCatalogPlugin(manifest);
        var updateResult =
            installer.InstallOrUpdateRepositoryPlugin(PluginId);

        Assert.True(updateResult.IsSuccess, updateResult.Error?.Message);
        Assert.Equal(
            "user data",
            File.ReadAllText(
                Path.Combine(installedDirectory, "data", "user.json")));
    }

    [Fact]
    public async Task InstallOrUpdateRepositoryPluginAsync_InstallsValidatedReleasePackage()
    {
        var entry = CreateEntry();
        entry.DistributionType = AppConstants.PluginDistributionRelease;
        entry.HasBackend = true;
        var catalogManifest = CreateReleaseManifest();
        WriteCatalogPlugin(catalogManifest);
        var packageManifest = CreateReleaseManifest();
        packageManifest.Distribution.Sha256 = null;
        packageManifest.Distribution.Size = null;
        var archivePath = WriteReleaseArchive(packageManifest);
        PluginPackageInfo? requestedPackage = null;
        var packageService = new Mock<IPluginPackageService>();
        packageService
            .Setup(service => service.DownloadPackageAsync(
                PluginId,
                It.IsAny<PluginPackageInfo>(),
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, PluginPackageInfo,
                IProgress<PluginDownloadProgress>?, CancellationToken>(
                (_, package, _, _) => requestedPackage = package)
            .ReturnsAsync(
                Result<DownloadedPluginPackage>.Success(
                    new DownloadedPluginPackage(archivePath, "github")));
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
        PluginManifest? installedManifest = null;
        var library = new Mock<IPluginLibrary>();
        library
            .Setup(service => service.InstallOrUpdateFromDirectory(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                AppConstants.PluginInstallSourceRepository))
            .Callback<string, IReadOnlyList<string>, string>(
                (directory, savedFiles, _) =>
                {
                    installedManifest = PluginManifest.LoadFromFile(
                        Path.Combine(
                            directory,
                            AppConstants.PluginManifestFileName)).Manifest;
                    Assert.True(
                        File.Exists(
                            Path.Combine(
                                directory,
                                "runtime",
                                "worker.exe")));
                    Assert.Equal(new[] { "data" }, savedFiles);
                })
            .Returns(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = PluginId,
                        Name = "Sample",
                        Version = "1.0.0"
                    }));
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions.Object,
            library.Object,
            packageService.Object,
            () => "1.4.0");

        var result =
            await installer.InstallOrUpdateRepositoryPluginAsync(PluginId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(installedManifest?.Companion);
        Assert.Equal(
            4321,
            installedManifest!.Companion!.ShutdownTimeoutMs);
        Assert.NotNull(requestedPackage);
        Assert.Equal(1024, requestedPackage!.Size);
        Assert.Equal(new string('a', 64), requestedPackage.Sha256);
        Assert.Equal(
            new[] { "github", "cnb" },
            requestedPackage.Sources.Select(source => source.Id));
        Assert.Contains(
            "/releases/download/sample-plugin-v1.0.0/",
            requestedPackage.Sources[0].Url);
        Assert.Contains(
            "/-/releases/download/sample-plugin-v1.0.0/",
            requestedPackage.Sources[1].Url);
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public async Task InstallOrUpdateRepositoryPluginAsync_RejectsReleaseManifestMismatch()
    {
        var entry = CreateEntry();
        entry.DistributionType = AppConstants.PluginDistributionRelease;
        entry.HasBackend = true;
        WriteCatalogPlugin(CreateReleaseManifest());
        var packageManifest = CreateReleaseManifest();
        packageManifest.Description = "tampered";
        packageManifest.Distribution.Sha256 = null;
        packageManifest.Distribution.Size = null;
        var archivePath = WriteReleaseArchive(packageManifest);
        var packageService = new Mock<IPluginPackageService>();
        packageService
            .Setup(service => service.DownloadPackageAsync(
                PluginId,
                It.IsAny<PluginPackageInfo>(),
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<DownloadedPluginPackage>.Success(
                    new DownloadedPluginPackage(archivePath, "github")));
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                entry))
            .Returns(
                Result<PluginSubscriptionRecord>.Success(
                    new PluginSubscriptionRecord { PluginId = PluginId }));
        var library = new Mock<IPluginLibrary>();
        var installer = new PluginInstaller(
            CreateRepositoryService(entry).Object,
            subscriptions.Object,
            library.Object,
            packageService.Object,
            () => "1.4.0");

        var result =
            await installer.InstallOrUpdateRepositoryPluginAsync(PluginId);

        Assert.True(result.IsFailure);
        Assert.Equal(
            PluginErrorCodes.RepositoryManifestInvalid,
            result.Error?.Code);
        library.Verify(
            service => service.InstallOrUpdateFromDirectory(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>()),
            Times.Never);
        Assert.False(File.Exists(archivePath));
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

    private static CatalogPluginDistribution CreateReleaseDistribution()
    {
        return new CatalogPluginDistribution {
            Type = AppConstants.PluginDistributionRelease,
            Tag = $"{PluginId}-v1.0.0",
            Asset = $"{PluginId}-1.0.0-win-x64.zip",
            Sha256 = new string('a', 64),
            Size = 1024
        };
    }

    private static CatalogPluginManifest CreateReleaseManifest()
    {
        var manifest = CreateManifest();
        manifest.Main = "frontend/main.js";
        manifest.Permissions = new List<string> {
            PluginPermissions.Companion
        };
        manifest.SavedFiles = new List<string> { "data/" };
        manifest.Distribution = CreateReleaseDistribution();
        manifest.Backend = new CatalogPluginBackend {
            Type = AppConstants.CompanionBackendType,
            Entry = "runtime/worker.exe",
            ProtocolVersion = AppConstants.CompanionProtocolVersion,
            Lifetime = AppConstants.CompanionLifetimePlugin,
            IntegrityLevel = AppConstants.CompanionIntegrityLevelInherit,
            ShutdownTimeoutMs = 4321
        };
        return manifest;
    }

    private string WriteReleaseArchive(
        CatalogPluginManifest packageManifest)
    {
        var archivePath = Path.Combine(
            _repositoryDirectory,
            $"release-{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(_repositoryDirectory);
        using var archive = ZipFile.Open(
            archivePath,
            ZipArchiveMode.Create);
        WriteArchiveEntry(
            archive,
            AppConstants.PluginRepositoryManifestFileName,
            JsonSerializer.Serialize(
                packageManifest,
                JsonHelper.WriteOptions));
        WriteArchiveEntry(archive, "frontend/main.js", "main");
        WriteArchiveEntry(archive, "runtime/worker.exe", "worker");
        return archivePath;
    }

    private static void WriteArchiveEntry(
        ZipArchive archive,
        string path,
        string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
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
