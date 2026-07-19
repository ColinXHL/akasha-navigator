using AkashaNavigator.ViewModels.Windows;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class PluginCenterViewModelTests
{
    [Fact]
    public void Constructor_DoesNotResolveDuplicatePageViewModels()
    {
        var constructor = Assert.Single(
            typeof(PluginCenterViewModel).GetConstructors());

        Assert.Empty(constructor.GetParameters());
    }

    [Theory]
    [InlineData(
        nameof(PluginCenterViewModel.NavigateToMyProfilesCommand),
        PluginCenterPageType.MyProfiles)]
    [InlineData(
        nameof(PluginCenterViewModel.NavigateToProfileMarketCommand),
        PluginCenterPageType.ProfileMarket)]
    [InlineData(
        nameof(PluginCenterViewModel.NavigateToInstalledPluginsCommand),
        PluginCenterPageType.InstalledPlugins)]
    [InlineData(
        nameof(PluginCenterViewModel.NavigateToAvailablePluginsCommand),
        PluginCenterPageType.AvailablePlugins)]
    public void NavigationCommand_ChangesCurrentPage(
        string commandPropertyName,
        PluginCenterPageType expectedPage)
    {
        var viewModel = new PluginCenterViewModel();
        var commandProperty =
            typeof(PluginCenterViewModel).GetProperty(commandPropertyName);
        var command = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(
            commandProperty?.GetValue(viewModel));

        command.Execute(null);

        Assert.Equal(expectedPage, viewModel.CurrentPage);
    }
}
