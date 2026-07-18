using System;
using System.IO;
using Xunit;

namespace AkashaNavigator.Tests;

public sealed class ReleaseWorkflowContractTests
{
    [Fact]
    public void UpdateNoticeWorkflow_ShouldAcceptAutomationPluginReleaseDispatch()
    {
        var workflow = File.ReadAllText(
            Path.Combine(GetRepositoryRoot(), ".github", "workflows", "update_notice.yml"));

        Assert.Contains("repository_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("automation_plugin_released", workflow, StringComparison.Ordinal);
        Assert.Contains("github.event.client_payload.version", workflow, StringComparison.Ordinal);
        Assert.Contains("PLUGIN_ONLY", workflow, StringComparison.Ordinal);
        Assert.Contains("if not plugin_only:", workflow, StringComparison.Ordinal);
        Assert.Contains("data[\"schemaVersion\"] = 2", workflow, StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AkashaNavigator.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the AkashaNavigator repository root.");
    }
}
