using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginInstallationTransactionTests : IDisposable
{
    private const string PluginId = "transaction-test";

    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            $"AkashaNavigator.PluginTransactionTests.{Guid.NewGuid():N}");
    private readonly string _libraryDirectory;
    private readonly string _indexPath;
    private readonly PluginLibrary _library;

    public PluginInstallationTransactionTests()
    {
        _libraryDirectory = Path.Combine(_root, "installed");
        _indexPath = Path.Combine(_root, "library.json");
        Directory.CreateDirectory(_libraryDirectory);
        _library = new PluginLibrary(
            _libraryDirectory,
            _indexPath,
            Path.Combine(_root, "builtin"),
            Mock.Of<ICompanionProcessManager>(),
            CreateConsentService());
    }

    [Fact]
    public void InstallOrUpdateFromDirectory_PreservesDeclaredSavedFiles()
    {
        var versionOne = CreateSource("1.0.0", "old code");
        var installResult = _library.InstallOrUpdateFromDirectory(
            versionOne,
            Array.Empty<string>(),
            AppConstants.PluginInstallSourceRepository);
        Assert.True(installResult.IsSuccess, installResult.Error?.Message);
        var installedDirectory = Path.Combine(_libraryDirectory, PluginId);
        File.WriteAllText(Path.Combine(installedDirectory, "config.json"), "user config");

        var versionTwo = CreateSource("2.0.0", "new code");
        File.WriteAllText(Path.Combine(versionTwo, "config.json"), "new default");
        var updateResult = _library.InstallOrUpdateFromDirectory(
            versionTwo,
            new[] { "config.json" },
            AppConstants.PluginInstallSourceRepository);

        Assert.True(updateResult.IsSuccess, updateResult.Error?.Message);
        Assert.Equal(
            "user config",
            File.ReadAllText(Path.Combine(installedDirectory, "config.json")));
        Assert.Equal(
            "new code",
            File.ReadAllText(Path.Combine(installedDirectory, "main.js")));
        Assert.Equal("2.0.0", _library.GetInstalledPluginInfo(PluginId)?.Version);
    }

    [Fact]
    public void InstallOrUpdateFromDirectory_WhenIndexCommitFails_RestoresOldVersion()
    {
        var versionOne = CreateSource("1.0.0", "old code");
        Assert.True(_library.InstallOrUpdateFromDirectory(
            versionOne,
            Array.Empty<string>(),
            AppConstants.PluginInstallSourceRepository).IsSuccess);
        File.Delete(_indexPath);
        Directory.CreateDirectory(_indexPath);

        var versionTwo = CreateSource("2.0.0", "new code");
        var result = _library.InstallOrUpdateFromDirectory(
            versionTwo,
            Array.Empty<string>(),
            AppConstants.PluginInstallSourceRepository);

        Assert.True(result.IsFailure);
        Assert.Equal(PluginErrorCodes.InstallTransactionFailed, result.Error!.Code);
        Assert.Equal(
            "old code",
            File.ReadAllText(
                Path.Combine(_libraryDirectory, PluginId, "main.js")));
        Assert.Equal("1.0.0", _library.GetInstalledPluginInfo(PluginId)?.Version);
    }

    [Fact]
    public void InstallOrUpdateFromDirectory_RejectsEscapingSavedFileWithoutMutation()
    {
        var versionOne = CreateSource("1.0.0", "old code");
        Assert.True(_library.InstallOrUpdateFromDirectory(
            versionOne,
            Array.Empty<string>(),
            AppConstants.PluginInstallSourceRepository).IsSuccess);
        var versionTwo = CreateSource("2.0.0", "new code");

        var result = _library.InstallOrUpdateFromDirectory(
            versionTwo,
            new[] { "../outside.txt" },
            AppConstants.PluginInstallSourceRepository);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "old code",
            File.ReadAllText(
                Path.Combine(_libraryDirectory, PluginId, "main.js")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_indexPath))
            {
                Directory.Delete(_indexPath);
            }

            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    private string CreateSource(string version, string mainContent)
    {
        var source = Path.Combine(_root, $"source-{version}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "main.js"), mainContent);
        File.WriteAllText(
            Path.Combine(source, AppConstants.PluginManifestFileName),
            JsonSerializer.Serialize(
                new
                {
                    id = PluginId,
                    name = "Transaction Test",
                    version,
                    main = "main.js",
                    permissions = Array.Empty<string>()
                }));
        return source;
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
