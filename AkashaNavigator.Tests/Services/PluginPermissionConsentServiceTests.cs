using System.IO;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginPermissionConsentServiceTests
{
    [Fact]
    public void Approval_ShouldPersistAndSatisfyFirstEnableWithoutPromptingAgain()
    {
        using var directory = new TemporaryDirectory();
        var consentPath = Path.Combine(directory.Path, "consents.json");
        var promptCount = 0;
        var installService = new PluginPermissionConsentService(
            consentPath,
            (_, _, operation) =>
            {
                promptCount++;
                Assert.Equal(PluginPermissionConsentOperation.Install, operation);
                return true;
            });
        var manifest = CreateManifest();

        Assert.True(installService.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.Install));
        Assert.True(File.Exists(consentPath));

        var firstEnableService = new PluginPermissionConsentService(
            consentPath,
            (_, _, _) => throw new InvalidOperationException("Persisted consent should avoid a second prompt."));
        Assert.True(firstEnableService.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.FirstEnable));
        Assert.Equal(1, promptCount);
    }

    [Fact]
    public void ChangedCompanionConfiguration_ShouldRequireNewApproval()
    {
        using var directory = new TemporaryDirectory();
        var consentPath = Path.Combine(directory.Path, "consents.json");
        var promptCount = 0;
        var allow = true;
        var service = new PluginPermissionConsentService(
            consentPath,
            (_, _, _) =>
            {
                promptCount++;
                return allow;
            });
        var manifest = CreateManifest();

        Assert.True(service.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.Install));

        manifest.Companion!.Executable = "worker/win-x64/Replacement.Worker.exe";
        allow = false;

        Assert.False(service.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.FirstEnable));
        Assert.Equal(2, promptCount);
    }

    [Fact]
    public void Revoke_ShouldRequireApprovalOnNextEnable()
    {
        using var directory = new TemporaryDirectory();
        var consentPath = Path.Combine(directory.Path, "consents.json");
        var promptCount = 0;
        var service = new PluginPermissionConsentService(
            consentPath,
            (_, _, _) =>
            {
                promptCount++;
                return true;
            });
        var manifest = CreateManifest();

        Assert.True(service.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.Install));
        Assert.True(service.RevokeHighRiskPermissionConsent(manifest.Id!));
        Assert.True(service.EnsureHighRiskPermissionsApproved(
            manifest,
            PluginPermissionConsentOperation.FirstEnable));
        Assert.Equal(2, promptCount);
    }

    [Fact]
    public void CorruptedConsentFile_ShouldFailClosedWithoutPrompting()
    {
        using var directory = new TemporaryDirectory();
        var consentPath = Path.Combine(directory.Path, "consents.json");
        File.WriteAllText(consentPath, "not-json");
        var promptCount = 0;
        var service = new PluginPermissionConsentService(
            consentPath,
            (_, _, _) =>
            {
                promptCount++;
                return true;
            });

        Assert.False(service.EnsureHighRiskPermissionsApproved(
            CreateManifest(),
            PluginPermissionConsentOperation.FirstEnable));
        Assert.Equal(0, promptCount);
    }

    private static PluginManifest CreateManifest() => new()
    {
        Id = "automation",
        Name = "Automation",
        Version = "1.0.0",
        Main = "main.js",
        Permissions = new List<string> { PluginPermissions.Companion },
        Companion = new CompanionManifest
        {
            Executable = "worker/win-x64/AkashaAutomation.Worker.exe"
        }
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"AkashaNavigator.PermissionConsentTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
