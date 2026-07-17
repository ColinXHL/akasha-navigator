using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Update;
using AkashaNavigator.ViewModels.Pages.Settings;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class AdvancedSettingsPageViewModelTests
{
    [Fact]
    public void LoadAndSaveSettings_PreservesDownloadSourcePreference()
    {
        var selector = new Mock<IDownloadSourceSelector>();
        var viewModel = CreateViewModel(selector.Object);
        var source = new AppConfig {
            AppUpdateSourcePreference = AppUpdateSourcePreference.GitHub,
            PluginDownloadSourcePreference = PluginDownloadSourcePreference.Cnb
        };

        viewModel.LoadSettings(source);
        var saved = new AppConfig();
        viewModel.SaveSettings(saved);

        Assert.Equal(AppUpdateSourcePreference.GitHub, viewModel.AppUpdateSourcePreference);
        Assert.Equal(AppUpdateSourcePreference.GitHub, saved.AppUpdateSourcePreference);
        Assert.Equal(PluginDownloadSourcePreference.Cnb, viewModel.PluginDownloadSourcePreference);
        Assert.Equal(PluginDownloadSourcePreference.Cnb, saved.PluginDownloadSourcePreference);
    }

    [Theory]
    [InlineData(AppUpdateSourcePreference.Cnb, false, "cnb")]
    [InlineData(AppUpdateSourcePreference.Cnb, true, "cnb-alpha")]
    [InlineData(AppUpdateSourcePreference.GitHub, false, "github")]
    [InlineData(AppUpdateSourcePreference.GitHub, true, "github")]
    public async Task CheckAppUpdateCommand_UsesSelectedApplicationSource(
        AppUpdateSourcePreference preference,
        bool isPrerelease,
        string expectedSourceId)
    {
        var updateService = new Mock<IAppUpdateService>();
        updateService
            .Setup(service => service.CheckForUpdateAsync(It.IsAny<bool>()))
            .ReturnsAsync(
                Result<AppUpdateCheckResult>.Success(
                    AppUpdateCheckResult.WithUpdate(
                        "1.0.0",
                        "2.0.0",
                        "notes",
                        isPrerelease,
                        "unused")));
        updateService
            .Setup(service => service.StartUpdater(expectedSourceId))
            .Returns(Result.Success());
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(service => service.ConfirmAsync(It.IsAny<string>(), "版本更新"))
            .ReturnsAsync(true);
        var viewModel = CreateViewModel(
            Mock.Of<IDownloadSourceSelector>(),
            updateService.Object,
            notificationService.Object);
        viewModel.AppUpdateSourcePreference = preference;

        await viewModel.CheckAppUpdateCommand.ExecuteAsync(null);

        updateService.Verify(service => service.StartUpdater(expectedSourceId), Times.Once);
    }

    [Fact]
    public async Task MeasureDownloadSourcesCommand_ProbesLatestInstallersAndShowsRecommendation()
    {
        var selector = new Mock<IDownloadSourceSelector>();
        PluginPackageInfo? measuredPackage = null;
        selector
            .Setup(service => service.MeasureSourcesAsync(
                It.IsAny<PluginPackageInfo>(),
                true,
                It.IsAny<CancellationToken>()))
            .Callback<PluginPackageInfo, bool, CancellationToken>(
                (package, _, _) => measuredPackage = package)
            .ReturnsAsync(
                Result<IReadOnlyList<DownloadSourceMeasurement>>.Success(
                    new[] {
                        new DownloadSourceMeasurement(
                            new DownloadSourceInfo { Id = "cnb", Url = "https://cnb.example.test/app.exe" },
                            true,
                            8 * 1024 * 1024,
                            4 * 1024 * 1024,
                            TimeSpan.FromMilliseconds(80),
                            TimeSpan.FromSeconds(20),
                            string.Empty),
                        new DownloadSourceMeasurement(
                            new DownloadSourceInfo { Id = "github", Url = "https://github.example.test/app.exe" },
                            true,
                            8 * 1024 * 1024,
                            2 * 1024 * 1024,
                            TimeSpan.FromMilliseconds(150),
                            TimeSpan.FromSeconds(40),
                            string.Empty)
                    }));
        var manifestService = new Mock<IUpdateManifestService>();
        manifestService
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<UpdateManifest>.Success(
                    new UpdateManifest {
                        Stable = new AppUpdateChannelInfo { Version = "1.4.0" }
                    }));
        var viewModel = CreateViewModel(
            selector.Object,
            manifestService: manifestService.Object);

        await viewModel.MeasureDownloadSourcesCommand.ExecuteAsync(null);

        Assert.NotNull(measuredPackage);
        Assert.Contains(
            measuredPackage!.Sources,
            source => source.Id == "cnb" &&
                      source.Url.Contains("/akasha-navigator/-/releases/download/v1.4.0/"));
        Assert.Contains(
            measuredPackage.Sources,
            source => source.Id == "github" &&
                      source.Url.Contains("/releases/download/v1.4.0/"));
        Assert.Contains("CNB：4.0 MiB/s", viewModel.DownloadSourceMeasurementStatus);
        Assert.Contains("推荐：CNB", viewModel.DownloadSourceMeasurementStatus);
        Assert.Contains("已应用到版本更新和插件下载", viewModel.DownloadSourceMeasurementStatus);
        Assert.Equal(
            AppUpdateSourcePreference.Cnb,
            viewModel.AppUpdateSourcePreference);
        Assert.Equal(
            PluginDownloadSourcePreference.Cnb,
            viewModel.PluginDownloadSourcePreference);
    }

    private static AdvancedSettingsPageViewModel CreateViewModel(
        IDownloadSourceSelector selector,
        IAppUpdateService? updateService = null,
        INotificationService? notificationService = null,
        IUpdateManifestService? manifestService = null)
    {
        return new AdvancedSettingsPageViewModel(
            updateService ?? Mock.Of<IAppUpdateService>(),
            notificationService ?? Mock.Of<INotificationService>(),
            Mock.Of<ILogService>(),
            selector,
            manifestService ?? Mock.Of<IUpdateManifestService>());
    }
}
