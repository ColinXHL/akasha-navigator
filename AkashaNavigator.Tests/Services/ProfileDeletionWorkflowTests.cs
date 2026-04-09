using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Profile;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public class ProfileDeletionWorkflowTests
{
    [Fact]
    public void PrepareDeletePlan_ReturnsUniquePluginSelections_WhenProfileOwnsPluginsExclusively()
    {
        var profileManager = new FakeProfileManager([
            new() { Id = "profile-1", Name = "Profile 1" }
        ]);
        var associationManager = new FakePluginAssociationManager(uniquePluginIds: ["plugin.alpha"]);
        var pluginLibrary = new FakePluginLibrary();
        pluginLibrary.InstallPlugin("plugin.alpha");

        var workflow = new ProfileDeletionWorkflow(
            profileManager,
            associationManager,
            pluginLibrary,
            new FakeNotificationService(),
            new RecordingEventBus());

        var plan = workflow.PrepareDeletePlan("profile-1");

        Assert.Equal("profile-1", plan.ProfileId);
        Assert.Single(plan.PluginChoices);
        Assert.Equal("plugin.alpha", plan.PluginChoices[0].PluginId);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_UninstallsSelectedPlugins_AndPublishesPluginEvents()
    {
        var profileManager = new FakeProfileManager([
            new() { Id = "profile-1", Name = "Profile 1" }
        ]);
        var associationManager = new FakePluginAssociationManager(uniquePluginIds: ["plugin.alpha"]);
        var pluginLibrary = new FakePluginLibrary();
        pluginLibrary.InstallPlugin("plugin.alpha");
        var notifications = new FakeNotificationService();
        var eventBus = new RecordingEventBus();
        var workflow = new ProfileDeletionWorkflow(
            profileManager,
            associationManager,
            pluginLibrary,
            notifications,
            eventBus);

        var plan = new DeleteProfilePlan
        {
            ProfileId = "profile-1",
            ProfileName = "Profile 1"
        };

        await workflow.ExecuteDeleteAsync(plan, ["plugin.alpha"]);

        Assert.Equal("profile-1", profileManager.LastDeletedProfileId);
        Assert.Contains("plugin.alpha", pluginLibrary.UninstalledPluginIds);
        Assert.Equal(2, eventBus.PublishedEvents.Count);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_ShowsError_WhenDeleteFails()
    {
        var profileManager = new FakeProfileManager([
            new() { Id = "profile-1", Name = "Profile 1" }
        ])
        {
            DeleteResult = Result.Failure("delete failed")
        };
        var notifications = new FakeNotificationService();
        var workflow = new ProfileDeletionWorkflow(
            profileManager,
            new FakePluginAssociationManager(),
            new FakePluginLibrary(),
            notifications,
            new RecordingEventBus());

        await workflow.ExecuteDeleteAsync(new DeleteProfilePlan
        {
            ProfileId = "profile-1",
            ProfileName = "Profile 1"
        }, []);

        Assert.Single(notifications.ErrorMessages);
    }
}
