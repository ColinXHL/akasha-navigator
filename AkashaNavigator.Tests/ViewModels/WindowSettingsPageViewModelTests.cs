using System.Collections.Generic;
using System.Linq;
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
    public void LoadSettings_WithNullCursorDetection_UsesDefaults()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var config = new AppConfig {
            EnableEdgeSnap = true, SnapThreshold = 15, PromptRecordOnExit = false,
            CursorDetection = null // 测试 null 情况
        };

        // Act
        viewModel.LoadSettings(config);

        // Assert - 基本设置
        Assert.True(viewModel.EnableEdgeSnap);
        Assert.Equal(15, viewModel.SnapThreshold);
        Assert.False(viewModel.PromptRecordOnExit);

        // Assert - CursorDetection 默认值
        Assert.False(viewModel.CursorDetectionEnabled);
        Assert.Equal(0.3, viewModel.CursorDetectionMinOpacity);
        Assert.Equal(200, viewModel.CursorDetectionCheckIntervalMs);
        Assert.False(viewModel.CursorDetectionEnableDebugLog);
        Assert.Empty(viewModel.ProcessWhitelist);
    }

    [Fact]
    public void LoadSettings_WithValidCursorDetection_LoadsCorrectly()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var config = new AppConfig { EnableEdgeSnap = false, SnapThreshold = 25, PromptRecordOnExit = true,
                                     CursorDetection = new GlobalCursorDetectionConfig {
                                         Enabled = true, MinOpacity = 0.5, CheckIntervalMs = 150, EnableDebugLog = true,
                                         ProcessWhitelist = new List<string> { "eldenring", "genshin" }
                                     } };

        // Act
        viewModel.LoadSettings(config);

        // Assert - 基本设置
        Assert.False(viewModel.EnableEdgeSnap);
        Assert.Equal(25, viewModel.SnapThreshold);
        Assert.True(viewModel.PromptRecordOnExit);

        // Assert - CursorDetection 设置
        Assert.True(viewModel.CursorDetectionEnabled);
        Assert.Equal(0.5, viewModel.CursorDetectionMinOpacity);
        Assert.Equal(150, viewModel.CursorDetectionCheckIntervalMs);
        Assert.True(viewModel.CursorDetectionEnableDebugLog);
        Assert.Equal(2, viewModel.ProcessWhitelist.Count);
        Assert.Contains("eldenring", viewModel.ProcessWhitelist);
        Assert.Contains("genshin", viewModel.ProcessWhitelist);
    }

    [Fact]
    public void SaveSettings_CreatesCorrectCursorDetectionConfig()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.EnableEdgeSnap = false;
        viewModel.SnapThreshold = 20;
        viewModel.PromptRecordOnExit = true;

        viewModel.CursorDetectionEnabled = true;
        viewModel.CursorDetectionMinOpacity = 0.4;
        viewModel.CursorDetectionCheckIntervalMs = 100;
        viewModel.CursorDetectionEnableDebugLog = true;
        viewModel.ProcessWhitelist.Add("testprocess");

        var config = new AppConfig();

        // Act
        viewModel.SaveSettings(config);

        // Assert - 基本设置
        Assert.False(config.EnableEdgeSnap);
        Assert.Equal(20, config.SnapThreshold);
        Assert.True(config.PromptRecordOnExit);

        // Assert - CursorDetection 设置
        Assert.NotNull(config.CursorDetection);
        Assert.True(config.CursorDetection.Enabled);
        Assert.Equal(0.4, config.CursorDetection.MinOpacity);
        Assert.Equal(100, config.CursorDetection.CheckIntervalMs);
        Assert.True(config.CursorDetection.EnableDebugLog);
        Assert.Single(config.CursorDetection.ProcessWhitelist);
        Assert.Contains("testprocess", config.CursorDetection.ProcessWhitelist);
    }

    [Fact]
    public void LoadThenSave_PreservesAllValues()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var originalConfig =
            new AppConfig { EnableEdgeSnap = false, SnapThreshold = 18, PromptRecordOnExit = true,
                            CursorDetection = new GlobalCursorDetectionConfig {
                                Enabled = true, MinOpacity = 0.6, CheckIntervalMs = 300, EnableDebugLog = false,
                                ProcessWhitelist = new List<string> { "game1", "game2", "game3" }
                            } };

        // Act - 加载
        viewModel.LoadSettings(originalConfig);

        // 保存到新配置对象
        var savedConfig = new AppConfig();
        viewModel.SaveSettings(savedConfig);

        // Assert - 验证值被正确保存
        Assert.Equal(originalConfig.EnableEdgeSnap, savedConfig.EnableEdgeSnap);
        Assert.Equal(originalConfig.SnapThreshold, savedConfig.SnapThreshold);
        Assert.Equal(originalConfig.PromptRecordOnExit, savedConfig.PromptRecordOnExit);

        Assert.NotNull(savedConfig.CursorDetection);
        Assert.Equal(originalConfig.CursorDetection.Enabled, savedConfig.CursorDetection.Enabled);
        Assert.Equal(originalConfig.CursorDetection.MinOpacity, savedConfig.CursorDetection.MinOpacity);
        Assert.Equal(originalConfig.CursorDetection.CheckIntervalMs, savedConfig.CursorDetection.CheckIntervalMs);
        Assert.Equal(originalConfig.CursorDetection.EnableDebugLog, savedConfig.CursorDetection.EnableDebugLog);
        Assert.Equal(3, savedConfig.CursorDetection.ProcessWhitelist.Count);
    }

    [Fact]
    public void SelectProcess_WithValidProcess_AddsToWhitelist()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var process = new RunningProcess { ProcessName = "testprocess", WindowTitle = "Test Window" };

        // Act
        viewModel.SelectedProcess = process;

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Contains("testprocess", viewModel.ProcessWhitelist);
        Assert.Null(viewModel.SelectedProcess); // 选择后应清空
    }

    [Fact]
    public void SelectProcess_WithDuplicate_DoesNotAdd()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.ProcessWhitelist.Add("existing");
        var process = new RunningProcess { ProcessName = "existing", WindowTitle = "Test Window" };

        // Act
        viewModel.SelectedProcess = process;

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Equal("existing", viewModel.ProcessWhitelist[0]);
    }

    [Fact]
    public void AddProcess_WithValidName_AddsToWhitelist()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.NewProcessName = "testprocess";

        // Act
        viewModel.AddProcessCommand.Execute(null);

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Contains("testprocess", viewModel.ProcessWhitelist);
        Assert.Empty(viewModel.NewProcessName);
    }

    [Fact]
    public void AddProcess_WithDuplicate_DoesNotAdd()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.ProcessWhitelist.Add("existing");
        viewModel.NewProcessName = "existing";

        // Act
        viewModel.AddProcessCommand.Execute(null);

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
    }

    [Fact]
    public void SelectProcessFromPopup_AddsToWhitelistAndClosesPopup()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        var process = new RunningProcess { ProcessName = "gameprocess", WindowTitle = "Game" };
        var popupClosed = false;
        viewModel.ClosePopupRequested += (s, e) => popupClosed = true;

        // Act
        viewModel.SelectProcessFromPopupCommand.Execute(process);

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
        Assert.Contains("gameprocess", viewModel.ProcessWhitelist);
        Assert.True(popupClosed);
    }

    [Fact]
    public void RemoveProcess_WithValidName_RemovesFromWhitelist()
    {
        // Arrange
        var viewModel = new WindowSettingsPageViewModel();
        viewModel.ProcessWhitelist.Add("process1");
        viewModel.ProcessWhitelist.Add("process2");

        // Act
        viewModel.RemoveProcessCommand.Execute("process1");

        // Assert
        Assert.Single(viewModel.ProcessWhitelist);
        Assert.DoesNotContain("process1", viewModel.ProcessWhitelist);
        Assert.Contains("process2", viewModel.ProcessWhitelist);
    }
}
