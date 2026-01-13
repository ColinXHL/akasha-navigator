using AkashaNavigator.Models.Config;
using AkashaNavigator.ViewModels.Pages.Settings;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

/// <summary>
/// WindowSettingsPageViewModel 单元测试
/// 测试窗口设置页面的加载和保存功能
/// </summary>
public class WindowSettingsPageViewModelTests
{
    [Fact]
    public void LoadSettings_WithValidConfig_LoadsCorrectly()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var config = new AppConfig { EnableEdgeSnap = true, SnapThreshold = 15, PromptRecordOnExit = false };

        // Act
        viewModel.LoadSettings(config);

        // Assert
        Assert.True(viewModel.EnableEdgeSnap);
        Assert.Equal(15, viewModel.SnapThreshold);
        Assert.False(viewModel.PromptRecordOnExit);
    }

    [Fact]
    public void LoadSettings_WithDifferentValues_LoadsCorrectly()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var config = new AppConfig { EnableEdgeSnap = false, SnapThreshold = 25, PromptRecordOnExit = true };

        // Act
        viewModel.LoadSettings(config);

        // Assert
        Assert.False(viewModel.EnableEdgeSnap);
        Assert.Equal(25, viewModel.SnapThreshold);
        Assert.True(viewModel.PromptRecordOnExit);
    }

    [Fact]
    public void SaveSettings_CreatesCorrectConfig()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.EnableEdgeSnap = false;
        viewModel.SnapThreshold = 20;
        viewModel.PromptRecordOnExit = true;

        var config = new AppConfig();

        // Act
        viewModel.SaveSettings(config);

        // Assert
        Assert.False(config.EnableEdgeSnap);
        Assert.Equal(20, config.SnapThreshold);
        Assert.True(config.PromptRecordOnExit);
    }

    [Fact]
    public void LoadThenSave_PreservesAllValues()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var originalConfig = new AppConfig { EnableEdgeSnap = false, SnapThreshold = 18, PromptRecordOnExit = true };

        // Act - 加载
        viewModel.LoadSettings(originalConfig);

        // 保存到新配置对象
        var savedConfig = new AppConfig();
        viewModel.SaveSettings(savedConfig);

        // Assert - 验证值被正确保存
        Assert.Equal(originalConfig.EnableEdgeSnap, savedConfig.EnableEdgeSnap);
        Assert.Equal(originalConfig.SnapThreshold, savedConfig.SnapThreshold);
        Assert.Equal(originalConfig.PromptRecordOnExit, savedConfig.PromptRecordOnExit);
    }

    [Fact]
    public void ResetSettings_RestoresOriginalValues()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var config = new AppConfig { EnableEdgeSnap = true, SnapThreshold = 30, PromptRecordOnExit = false };

        // 先加载配置
        viewModel.LoadSettings(config);

        // 修改值
        viewModel.EnableEdgeSnap = false;
        viewModel.SnapThreshold = 10;
        viewModel.PromptRecordOnExit = true;

        // Act - 重置
        viewModel.ResetSettings(config);

        // Assert - 验证值被重置
        Assert.True(viewModel.EnableEdgeSnap);
        Assert.Equal(30, viewModel.SnapThreshold);
        Assert.False(viewModel.PromptRecordOnExit);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var viewModel = new WindowSettingsPageViewModel();

        // Assert - 默认值应该是 false/0
        Assert.False(viewModel.EnableEdgeSnap);
        Assert.Equal(0, viewModel.SnapThreshold);
        Assert.False(viewModel.PromptRecordOnExit);
    }
}
