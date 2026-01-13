using System;
using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;
using Xunit;

namespace AkashaNavigator.Tests.Services
{
/// <summary>
/// ConfigService 单元测试
/// 测试配置加载、保存、默认值处理和错误处理
/// </summary>
public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configFilePath;
    private readonly ILogService _logService;
    private readonly TestableConfigService _configService;

    public ConfigServiceTests()
    {
        // 创建临时目录用于测试
        _tempDir = Path.Combine(Path.GetTempPath(), $"akasha_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _configFilePath = Path.Combine(_tempDir, "config.json");

        // 创建测试用的服务
        _logService = new LogService(Path.Combine(_tempDir, "Logs"));
        _configService = new TestableConfigService(_logService, _configFilePath);
    }

    public void Dispose()
    {
        // 清理临时目录
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }

#region Load / Save Tests - 1.2.1

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultConfig()
    {
        // Arrange - 确保配置文件不存在
        if (File.Exists(_configFilePath))
        {
            File.Delete(_configFilePath);
        }

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);
        Assert.Equal(AppConstants.DefaultProfileId, config.CurrentProfileId);
        Assert.True(config.IsFirstLaunch);
        Assert.False(config.EnableDebugLog);
        Assert.True(config.EnablePluginUpdateNotification);
        Assert.True(config.EnableEdgeSnap);
        Assert.Equal(AppConstants.SnapThreshold, config.SnapThreshold);
        Assert.False(config.PromptRecordOnExit);
    }

    [Fact]
    public void Load_WithValidConfigFile_ReturnsCorrectConfig()
    {
        // Arrange - 创建配置文件
        var expectedConfig = new AppConfig { SeekSeconds = 10,
                                             CurrentProfileId = "custom_profile",
                                             IsFirstLaunch = false,
                                             EnableDebugLog = true,
                                             EnablePluginUpdateNotification = false,
                                             EnableEdgeSnap = false,
                                             SnapThreshold = 20,
                                             PromptRecordOnExit = true };
        var json = JsonSerializer.Serialize(expectedConfig, JsonHelper.WriteOptions);
        File.WriteAllText(_configFilePath, json);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(10, config.SeekSeconds);
        Assert.Equal("custom_profile", config.CurrentProfileId);
        Assert.False(config.IsFirstLaunch);
        Assert.True(config.EnableDebugLog);
        Assert.False(config.EnablePluginUpdateNotification);
        Assert.False(config.EnableEdgeSnap);
        Assert.Equal(20, config.SnapThreshold);
        Assert.True(config.PromptRecordOnExit);
    }

    [Fact]
    public void Save_WritesConfigToFile()
    {
        // Arrange
        _configService.Config.SeekSeconds = 15;
        _configService.Config.CurrentProfileId = "test_profile";

        // Act
        _configService.TestSave();

        // Assert - 验证文件存在
        Assert.True(File.Exists(_configFilePath));

        // 验证文件内容
        var json = File.ReadAllText(_configFilePath);
        var savedConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonHelper.ReadOptions);

        Assert.NotNull(savedConfig);
        Assert.Equal(15, savedConfig.SeekSeconds);
        Assert.Equal("test_profile", savedConfig.CurrentProfileId);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        // Arrange - 使用不存在的目录
        var nestedDir = Path.Combine(_tempDir, "Nested", "Dir");
        var nestedConfigPath = Path.Combine(nestedDir, "config.json");
        var nestedService = new TestableConfigService(_logService, nestedConfigPath);

        nestedService.Config.SeekSeconds = 20;

        // Act
        nestedService.TestSave();

        // Assert
        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(nestedConfigPath));

        var json = File.ReadAllText(nestedConfigPath);
        var savedConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonHelper.ReadOptions);
        Assert.Equal(20, savedConfig?.SeekSeconds);
    }

    [Fact]
    public void Save_ThenLoad_PreservesConfig()
    {
        // Arrange
        _configService.Config.SeekSeconds = 12;
        _configService.Config.CurrentProfileId = "preserved_profile";
        _configService.Config.IsFirstLaunch = false;
        _configService.Config.EnableDebugLog = true;

        // Act
        _configService.TestSave();
        var newService = new TestableConfigService(_logService, _configFilePath);
        var loadedConfig = newService.Config;

        // Assert
        Assert.Equal(12, loadedConfig.SeekSeconds);
        Assert.Equal("preserved_profile", loadedConfig.CurrentProfileId);
        Assert.False(loadedConfig.IsFirstLaunch);
        Assert.True(loadedConfig.EnableDebugLog);
    }

    [Fact]
    public void Save_CamelCasePropertyNames_WritesCorrectly()
    {
        // Arrange
        _configService.Config.SeekSeconds = 5;

        // Act
        _configService.TestSave();
        var json = File.ReadAllText(_configFilePath);

        // Assert - 验证属性名为 camelCase
        Assert.Contains("seekSeconds", json);
        Assert.Contains("currentProfileId", json);
    }

#endregion

#region Default Values Tests - 1.2.2

    [Fact]
    public void Constructor_WithNoFile_LoadsDefaultConfig()
    {
        // Arrange - 确保配置文件不存在
        if (File.Exists(_configFilePath))
        {
            File.Delete(_configFilePath);
        }

        // Act
        var service = new TestableConfigService(_logService, _configFilePath);

        // Assert - 验证所有默认值
        var config = service.Config;
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);

        // 热键默认值
        Assert.Equal(Win32Helper.VK_6, config.HotkeySeekForward);
        Assert.Equal(ModifierKeys.None, config.HotkeySeekForwardMod);
        Assert.Equal(Win32Helper.VK_5, config.HotkeySeekBackward);
        Assert.Equal(ModifierKeys.None, config.HotkeySeekBackwardMod);
        Assert.Equal(Win32Helper.VK_OEM_3, config.HotkeyTogglePlay);
        Assert.Equal(ModifierKeys.None, config.HotkeyTogglePlayMod);
        Assert.Equal(Win32Helper.VK_8, config.HotkeyIncreaseOpacity);
        Assert.Equal(ModifierKeys.None, config.HotkeyIncreaseOpacityMod);
        Assert.Equal(Win32Helper.VK_7, config.HotkeyDecreaseOpacity);
        Assert.Equal(ModifierKeys.None, config.HotkeyDecreaseOpacityMod);
        Assert.Equal(Win32Helper.VK_0, config.HotkeyToggleClickThrough);
        Assert.Equal(ModifierKeys.None, config.HotkeyToggleClickThroughMod);
        Assert.Equal((uint)0x0D, config.HotkeyToggleMaximize); // VK_RETURN
        Assert.Equal(ModifierKeys.Alt, config.HotkeyToggleMaximizeMod);

        // Profile 默认值
        Assert.Equal(AppConstants.DefaultProfileId, config.CurrentProfileId);

        // 启动默认值
        Assert.True(config.IsFirstLaunch);

        // 日志默认值
        Assert.False(config.EnableDebugLog);

        // 插件更新默认值
        Assert.True(config.EnablePluginUpdateNotification);

        // 窗口行为默认值
        Assert.True(config.EnableEdgeSnap);
        Assert.Equal(AppConstants.SnapThreshold, config.SnapThreshold);
        Assert.False(config.PromptRecordOnExit);
    }

    [Fact]
    public void ResetToDefault_RestoresAllDefaultValues()
    {
        // Arrange - 修改配置
        _configService.Config.SeekSeconds = 99;
        _configService.Config.CurrentProfileId = "modified";
        _configService.Config.IsFirstLaunch = false;
        _configService.Config.EnableDebugLog = true;
        _configService.Config.EnableEdgeSnap = false;

        // Act
        _configService.ResetToDefault();

        // Assert
        Assert.Equal(AppConstants.DefaultSeekSeconds, _configService.Config.SeekSeconds);
        Assert.Equal(AppConstants.DefaultProfileId, _configService.Config.CurrentProfileId);
        Assert.True(_configService.Config.IsFirstLaunch);
        Assert.False(_configService.Config.EnableDebugLog);
        Assert.True(_configService.Config.EnableEdgeSnap);
    }

    [Fact]
    public void ResetToDefault_SavesToFile()
    {
        // Arrange
        _configService.Config.SeekSeconds = 99;

        // Act
        _configService.ResetToDefault();

        // Assert - 创建新服务实例验证文件已保存
        var newService = new TestableConfigService(_logService, _configFilePath);
        Assert.Equal(AppConstants.DefaultSeekSeconds, newService.Config.SeekSeconds);
    }

    [Fact]
    public void UpdateConfig_WithPartialConfig_UpdatesOnlyProvidedProperties()
    {
        // Arrange
        _configService.Config.SeekSeconds = 10;

        var newConfig = new AppConfig { SeekSeconds = 20 };

        // Act
        _configService.UpdateConfig(newConfig);

        // Assert
        Assert.Equal(20, _configService.Config.SeekSeconds);
    }

#endregion

#region Migration Compatibility Tests - 1.2.3

    [Fact]
    public void Load_WithPascalCaseProperties_DeserializesCorrectly()
    {
        // Arrange - 创建使用 PascalCase 属性名的旧格式配置文件
        var pascalCaseJson = @"{
            ""SeekSeconds"": 25,
            ""CurrentProfileId"": ""old_format"",
            ""IsFirstLaunch"": false
        }";
        File.WriteAllText(_configFilePath, pascalCaseJson);

        // Act
        var config = _configService.TestLoad();

        // Assert - JsonHelper.ReadOptions 设置了 PropertyNameCaseInsensitive = true
        Assert.NotNull(config);
        Assert.Equal(25, config.SeekSeconds);
        Assert.Equal("old_format", config.CurrentProfileId);
        Assert.False(config.IsFirstLaunch);
    }

    [Fact]
    public void Load_WithMissingProperties_UsesDefaults()
    {
        // Arrange - 创建缺少某些属性的配置文件
        var partialJson = @"{
            ""seekSeconds"": 30
        }";
        File.WriteAllText(_configFilePath, partialJson);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.Equal(30, config.SeekSeconds);
        // 缺失的属性使用默认值
        Assert.Equal(AppConstants.DefaultProfileId, config.CurrentProfileId);
        Assert.True(config.IsFirstLaunch);
    }

    [Fact]
    public void Load_WithExtraProperties_IgnoresExtras()
    {
        // Arrange - 创建包含额外属性的配置文件（未来版本兼容性）
        var jsonWithExtras = @"{
            ""seekSeconds"": 10,
            ""futureProperty"": ""some value"",
            ""anotherFutureProp"": 123
        }";
        File.WriteAllText(_configFilePath, jsonWithExtras);

        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() => _configService.TestLoad());

        Assert.Null(exception);
    }

    [Fact]
    public void Load_WithHotkeyConfig_DeserializesCorrectly()
    {
        // Arrange
        var hotkeyJson = @"{
            ""hotkeySeekForward"": 65,
            ""hotkeySeekForwardMod"": 2,
            ""hotkeySeekBackward"": 66,
            ""hotkeyTogglePlay"": 67,
            ""hotkeyIncreaseOpacity"": 68,
            ""hotkeyDecreaseOpacity"": 69,
            ""hotkeyToggleClickThrough"": 70,
            ""hotkeyToggleMaximize"": 13,
            ""hotkeyToggleMaximizeMod"": 1
        }";
        File.WriteAllText(_configFilePath, hotkeyJson);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.Equal((uint)65, config.HotkeySeekForward);
        Assert.Equal(ModifierKeys.Ctrl, config.HotkeySeekForwardMod); // 2 = Ctrl
        Assert.Equal((uint)66, config.HotkeySeekBackward);
        Assert.Equal((uint)67, config.HotkeyTogglePlay);
        Assert.Equal((uint)68, config.HotkeyIncreaseOpacity);
        Assert.Equal((uint)69, config.HotkeyDecreaseOpacity);
        Assert.Equal((uint)70, config.HotkeyToggleClickThrough);
        Assert.Equal((uint)13, config.HotkeyToggleMaximize);            // VK_RETURN
        Assert.Equal(ModifierKeys.Alt, config.HotkeyToggleMaximizeMod); // 1 = Alt
    }

#endregion

#region Invalid File Handling Tests - 1.2.4

    [Fact]
    public void Load_WithInvalidJson_ReturnsDefaultConfig()
    {
        // Arrange - 创建无效的 JSON 文件
        var invalidJson = @"{ ""seekSeconds"": invalid }";
        File.WriteAllText(_configFilePath, invalidJson);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);
    }

    [Fact]
    public void Load_WithEmptyFile_ReturnsDefaultConfig()
    {
        // Arrange
        File.WriteAllText(_configFilePath, string.Empty);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);
    }

    [Fact]
    public void Load_WithNonJsonContent_ReturnsDefaultConfig()
    {
        // Arrange - 创建非 JSON 内容的文件
        File.WriteAllText(_configFilePath, "This is not JSON at all.");

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);
    }

    [Fact]
    public void Load_WithMalformedJsonArray_ReturnsDefaultConfig()
    {
        // Arrange - JSON 数组而非对象
        var jsonArray = @"[ ""item1"", ""item2"" ]";
        File.WriteAllText(_configFilePath, jsonArray);

        // Act
        var config = _configService.TestLoad();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(AppConstants.DefaultSeekSeconds, config.SeekSeconds);
    }

    [Fact]
    public void Save_WithInvalidPath_DoesNotCrash()
    {
        // Arrange - 使用无效路径
        var invalidService = new TestableConfigService(_logService, "X:\\|Invalid?Path|/config.json");

        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() => invalidService.TestSave());

        Assert.Null(exception);
    }

    [Fact]
    public void Load_ToHotkeyConfig_ConvertsCorrectly()
    {
        // Arrange
        var config = new AppConfig {
            HotkeySeekForward = Win32Helper.VK_6,
            HotkeySeekForwardMod = ModifierKeys.None,
            HotkeySeekBackward = Win32Helper.VK_5,
            HotkeySeekBackwardMod = ModifierKeys.None,
        };

        // Act
        var hotkeyConfig = config.ToHotkeyConfig();

        // Assert
        Assert.NotNull(hotkeyConfig);
        Assert.Equal("Default", hotkeyConfig.ActiveProfileName);
        Assert.False(hotkeyConfig.AutoSwitchProfile);
        Assert.Single(hotkeyConfig.Profiles);
        Assert.Equal("Default", hotkeyConfig.Profiles[0].Name);
        Assert.NotNull(hotkeyConfig.Profiles[0].Bindings);
        // 13 bindings: SeekBackward, SeekForward, TogglePlay, DecreaseOpacity, IncreaseOpacity,
        // ResetOpacity, ToggleClickThrough, DecreasePlaybackRate, IncreasePlaybackRate,
        // ResetPlaybackRate, ToggleWindowVisibility, SuspendHotkeys, ToggleMaximize
        Assert.Equal(13, hotkeyConfig.Profiles[0].Bindings.Count);
    }

    [Fact]
    public void Constructor_WithNullLogService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>("logService", () => new TestableConfigService(null!, _configFilePath));
    }

    [Fact]
    public void UpdateConfig_RaisesConfigChangedEvent()
    {
        // Arrange
        AppConfig? receivedConfig = null;
        _configService.ConfigChanged += (sender, config) => receivedConfig = config;

        var newConfig = new AppConfig { SeekSeconds = 100 };

        // Act
        _configService.UpdateConfig(newConfig);

        // Assert
        Assert.NotNull(receivedConfig);
        Assert.Equal(100, receivedConfig.SeekSeconds);
    }

    [Fact]
    public void ResetToDefault_RaisesConfigChangedEvent()
    {
        // Arrange
        AppConfig? receivedConfig = null;
        _configService.ConfigChanged += (sender, config) => receivedConfig = config;

        // Act
        _configService.ResetToDefault();

        // Assert
        Assert.NotNull(receivedConfig);
        Assert.Equal(AppConstants.DefaultSeekSeconds, receivedConfig.SeekSeconds);
    }

#endregion

#region Testable ConfigService

    /// <summary>
    /// 可测试的 ConfigService 版本，允许指定配置文件路径
    /// </summary>
    private class TestableConfigService : ConfigService
    {
        private readonly string _testConfigPath;
        private readonly ILogService _logService;

        public TestableConfigService(ILogService logService, string configFilePath) : base(logService)
        {
            _testConfigPath = configFilePath;
            _logService = logService;
            // 基类构造函数会调用 Load()，但使用的是 AppPaths.ConfigFilePath
            // 所以我们需要重新从测试路径加载配置
            ReloadFromTestPath();
        }

        private void ReloadFromTestPath()
        {
            var result = JsonHelper.LoadFromFile<AppConfig>(_testConfigPath);
            if (result.IsSuccess)
            {
                SetConfig(result.Value!);
            }
            else
            {
                // 文件不存在或加载失败，使用默认配置
                SetConfig(new AppConfig());
            }
        }

        public AppConfig TestLoad()
        {
            // 使用指定的测试路径而不是 AppPaths.ConfigFilePath
            var result = JsonHelper.LoadFromFile<AppConfig>(_testConfigPath);
            if (result.IsSuccess)
            {
                SetConfig(result.Value!);
                return result.Value!;
            }

            _logService.Warn(nameof(ConfigService), "加载配置失败，将使用默认配置: {ErrorMessage}",
                             result.Error?.Message ?? "未知错误");
            var defaultConfig = new AppConfig();
            SetConfig(defaultConfig);
            return defaultConfig;
        }

        public void TestSave()
        {
            var result = JsonHelper.SaveToFile(_testConfigPath, Config);
            if (result.IsFailure)
            {
                _logService.Debug(nameof(ConfigService), "保存配置失败: {ErrorMessage}",
                                  result.Error?.Message ?? "未知错误");
            }
        }

        // 使用反射设置 Config 属性（因为它是私有的 setter）
        private void SetConfig(AppConfig config)
        {
            var property = typeof(ConfigService).GetProperty(nameof(Config));
            property?.SetValue(this, config);
        }
    }

#endregion
}
}
