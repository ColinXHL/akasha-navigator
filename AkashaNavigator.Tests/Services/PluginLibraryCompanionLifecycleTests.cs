using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginLibraryCompanionLifecycleTests
{
    [Fact]
    public void Uninstall_ShouldStopCompanionAndRevokeConsentBeforeDeletingDirectory()
    {
        using var environment = new PluginLibraryTestEnvironment();
        environment.InstallVersion("1.0.0", "old.txt");
        var pluginDirectory = environment.InstalledPluginDirectory;

        var result = environment.Library.UninstallPlugin(environment.PluginId, force: true);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(environment.ProcessManager.DirectoryExistedWhenStopped);
        Assert.True(environment.ConsentService.DirectoryExistedWhenRevoked);
        Assert.False(Directory.Exists(pluginDirectory));
    }

    [Fact]
    public void Update_ShouldStopCompanionBeforeReplacingPluginFiles()
    {
        using var environment = new PluginLibraryTestEnvironment();
        environment.InstallVersion("1.0.0", "old.txt");
        environment.PublishBuiltInVersion("2.0.0", "new.txt");

        var result = environment.Library.UpdatePlugin(environment.PluginId);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(environment.ProcessManager.DirectoryExistedWhenStopped);
        Assert.True(environment.ProcessManager.OldMarkerExistedWhenStopped);
        Assert.False(File.Exists(Path.Combine(environment.InstalledPluginDirectory, "old.txt")));
        Assert.True(File.Exists(Path.Combine(environment.InstalledPluginDirectory, "new.txt")));
        Assert.Contains(PluginPermissionConsentOperation.Update, environment.ConsentService.Operations);
    }

    [Fact]
    public void Install_ShouldRejectUnsafePluginIdBeforeResolvingAnyTargetPath()
    {
        using var environment = new PluginLibraryTestEnvironment();
        var outsidePath = Path.Combine(environment.RootDirectory, "outside");
        var unsafeIds = new[] { "../outside", "folder/plugin", "folder\\plugin", outsidePath };

        foreach (var unsafeId in unsafeIds)
        {
            var result = environment.Library.InstallPlugin(unsafeId);

            Assert.False(result.IsSuccess);
        }

        Assert.False(Directory.Exists(outsidePath));
    }

    [Fact]
    public void Update_ShouldRejectMismatchedManifestBeforeConsentStopOrFileMutation()
    {
        using var environment = new PluginLibraryTestEnvironment();
        environment.InstallVersion("1.0.0", "old.txt");
        environment.PublishBuiltInVersion("2.0.0", "new.txt", manifestId: "different-plugin");

        var result = environment.Library.UpdatePlugin(environment.PluginId);

        Assert.False(result.IsSuccess);
        Assert.Contains("ID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(environment.ConsentService.Operations);
        Assert.Equal(0, environment.ProcessManager.StopCallCount);
        Assert.True(File.Exists(Path.Combine(environment.InstalledPluginDirectory, "old.txt")));
        Assert.False(File.Exists(Path.Combine(environment.InstalledPluginDirectory, "new.txt")));
    }

    private sealed class PluginLibraryTestEnvironment : IDisposable
    {
        private readonly string _root;
        private readonly string _libraryDirectory;
        private readonly string _builtInDirectory;
        private readonly string _indexPath;

        public PluginLibraryTestEnvironment()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                $"AkashaNavigator.PluginLibraryCompanionTests.{Guid.NewGuid():N}");
            _libraryDirectory = Path.Combine(_root, "installed");
            _builtInDirectory = Path.Combine(_root, "builtin");
            _indexPath = Path.Combine(_root, "library.json");
            Directory.CreateDirectory(_libraryDirectory);
            Directory.CreateDirectory(_builtInDirectory);
            ProcessManager = new RecordingCompanionProcessManager(this);
            ConsentService = new RecordingConsentService(this);
            Library = new PluginLibrary(
                _libraryDirectory,
                _indexPath,
                _builtInDirectory,
                ProcessManager,
                ConsentService);
        }

        public string PluginId => "companion-lifecycle-test";

        public string RootDirectory => _root;

        public string InstalledPluginDirectory => Path.Combine(_libraryDirectory, PluginId);

        public PluginLibrary Library { get; private set; }

        public RecordingCompanionProcessManager ProcessManager { get; }

        public RecordingConsentService ConsentService { get; }

        public void InstallVersion(string version, string markerFile)
        {
            WritePlugin(InstalledPluginDirectory, version, markerFile);
            new PluginLibraryIndex
            {
                Plugins = new List<InstalledPluginEntry>
                {
                    new()
                    {
                        Id = PluginId,
                        Version = version,
                        Source = "external"
                    }
                }
            }.SaveToFile(_indexPath);
            Library = new PluginLibrary(
                _libraryDirectory,
                _indexPath,
                _builtInDirectory,
                ProcessManager,
                ConsentService);
        }

        public void PublishBuiltInVersion(string version, string markerFile, string? manifestId = null)
        {
            WritePlugin(Path.Combine(_builtInDirectory, PluginId), version, markerFile, manifestId);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
            }
        }

        private void WritePlugin(string directory, string version, string markerFile, string? manifestId = null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, markerFile), markerFile);
            File.WriteAllText(Path.Combine(directory, "main.js"), string.Empty);
            File.WriteAllText(
                Path.Combine(directory, "plugin.json"),
                JsonSerializer.Serialize(new
                {
                    id = manifestId ?? PluginId,
                    name = "Companion Lifecycle Test",
                    version,
                    main = "main.js",
                    permissions = new[] { "companion" },
                    companion = new
                    {
                        executable = "worker/win-x64/Worker.exe",
                        protocolVersion = 1,
                        lifetime = "plugin",
                        singleInstance = true
                    }
                }));
        }

        public sealed class RecordingCompanionProcessManager : ICompanionProcessManager
        {
            private readonly PluginLibraryTestEnvironment _environment;

            public RecordingCompanionProcessManager(PluginLibraryTestEnvironment environment)
            {
                _environment = environment;
            }

            public bool DirectoryExistedWhenStopped { get; private set; }

            public bool OldMarkerExistedWhenStopped { get; private set; }

            public int StopCallCount { get; private set; }

            public Task<CompanionStatus> StartAsync(
                string pluginId,
                string pluginDirectory,
                CompanionManifest manifest,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new CompanionStatus(true, "running", 1));

            public Task<JsonElement?> InvokeAsync(
                string pluginId,
                string method,
                JsonElement? payload,
                CancellationToken cancellationToken = default) => Task.FromResult(payload);

            public CompanionStatus GetStatus(string pluginId) => new(false, "stopped");

            public Task StopAsync(string pluginId, CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                DirectoryExistedWhenStopped = Directory.Exists(_environment.InstalledPluginDirectory);
                OldMarkerExistedWhenStopped =
                    File.Exists(Path.Combine(_environment.InstalledPluginDirectory, "old.txt"));
                return Task.CompletedTask;
            }

            public Task StopAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Dispose() { }
        }

        public sealed class RecordingConsentService : IPluginPermissionConsentService
        {
            private readonly PluginLibraryTestEnvironment _environment;

            public RecordingConsentService(PluginLibraryTestEnvironment environment)
            {
                _environment = environment;
            }

            public List<PluginPermissionConsentOperation> Operations { get; } = new();

            public bool DirectoryExistedWhenRevoked { get; private set; }

            public bool EnsureHighRiskPermissionsApproved(
                PluginManifest manifest,
                PluginPermissionConsentOperation operation)
            {
                Operations.Add(operation);
                return true;
            }

            public bool RevokeHighRiskPermissionConsent(string pluginId)
            {
                DirectoryExistedWhenRevoked = Directory.Exists(_environment.InstalledPluginDirectory);
                return true;
            }
        }
    }
}
