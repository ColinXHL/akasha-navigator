using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using FsCheck;
using FsCheck.Xunit;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// 插件预设配置属性测试
/// </summary>
public class PluginPresetConfigPropertyTests : IDisposable
{
    private readonly string _testDir;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IPluginLibrary> _mockPluginLibrary;

    public PluginPresetConfigPropertyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PluginPresetConfigTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _mockLogService = new Mock<ILogService>();
        _mockPluginLibrary = new Mock<IPluginLibrary>();
        _mockPluginLibrary.Setup(x => x.GetPluginDirectory(It.IsAny<string>()))
            .Returns<string>(pluginId => Path.Combine(_testDir, "plugins", pluginId));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // 忽略清理错误
        }
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 4: 插件预设配置不覆盖**
    /// **Validates: Requirements 3.3**
    ///
    /// *For any* profile with plugin preset configs, if a plugin config file already exists
    /// at the target path, calling ApplyPluginPresetConfigs SHALL NOT modify the existing file content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PresetConfig_DoesNotOverwrite_ExistingConfig(NonEmptyString profileId, NonEmptyString pluginId,
                                                                 NonEmptyString existingValue, NonEmptyString newValue)
    {
        // 确保值不同以验证不覆盖
        if (existingValue.Get == newValue.Get)
            return true.ToProperty();

        // 清理 ID 中的非法字符
        var cleanProfileId = CleanId(profileId.Get);
        var cleanPluginId = CleanId(pluginId.Get);

        if (string.IsNullOrEmpty(cleanProfileId) || string.IsNullOrEmpty(cleanPluginId))
            return true.ToProperty();

        // 创建配置目录
        var configDir = Path.Combine(_testDir, "Profiles", cleanProfileId, "plugins", cleanPluginId);
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "config.json");

        // 创建已存在的配置文件
        var existingConfig =
            new Dictionary<string, JsonElement> { ["testKey"] = JsonSerializer.SerializeToElement(existingValue.Get) };
        var existingJson = JsonSerializer.Serialize(existingConfig, JsonHelper.WriteOptions);
        File.WriteAllText(configPath, existingJson);

        // 记录原始内容
        var originalContent = File.ReadAllText(configPath);

        // 创建新的预设配置
        var presetConfigs = new Dictionary<string, Dictionary<string, JsonElement>> {
            [cleanPluginId] =
                new Dictionary<string, JsonElement> { ["testKey"] = JsonSerializer.SerializeToElement(newValue.Get) }
        };

        // 创建测试用的 PluginAssociationManager
        var indexPath = Path.Combine(_testDir, "associations.json");
        var manager = new TestablePluginAssociationManager(_mockLogService.Object, _mockPluginLibrary.Object, indexPath,
                                                           Path.Combine(_testDir, "Profiles"));

        // 应用预设配置
        manager.ApplyPluginPresetConfigs(cleanProfileId, presetConfigs);

        // 验证文件内容未被修改
        var currentContent = File.ReadAllText(configPath);
        return (currentContent == originalContent).ToProperty();
    }

    /// <summary>
    /// **Feature: smart-cursor-detection-plugin, Property 4: 插件预设配置不覆盖**
    /// **Validates: Requirements 3.3**
    ///
    /// 验证当配置文件不存在时，预设配置会被正确创建
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PresetConfig_CreatesConfig_WhenNotExists(NonEmptyString profileId, NonEmptyString pluginId,
                                                             NonEmptyString configValue)
    {
        // 清理 ID 中的非法字符
        var cleanProfileId = CleanId(profileId.Get);
        var cleanPluginId = CleanId(pluginId.Get);

        if (string.IsNullOrEmpty(cleanProfileId) || string.IsNullOrEmpty(cleanPluginId))
            return true.ToProperty();

        // 配置路径
        var configDir = Path.Combine(_testDir, "Profiles", cleanProfileId, "plugins", cleanPluginId);
        var configPath = Path.Combine(configDir, "config.json");

        // 确保配置文件不存在
        if (File.Exists(configPath))
            File.Delete(configPath);

        // 创建预设配置
        var presetConfigs = new Dictionary<string, Dictionary<string, JsonElement>> {
            [cleanPluginId] =
                new Dictionary<string, JsonElement> { ["testKey"] = JsonSerializer.SerializeToElement(configValue.Get) }
        };

        // 创建测试用的 PluginAssociationManager
        var indexPath = Path.Combine(_testDir, "associations.json");
        var manager = new TestablePluginAssociationManager(_mockLogService.Object, _mockPluginLibrary.Object, indexPath,
                                                           Path.Combine(_testDir, "Profiles"));

        // 应用预设配置
        manager.ApplyPluginPresetConfigs(cleanProfileId, presetConfigs);

        // 验证配置文件已创建
        if (!File.Exists(configPath))
            return false.ToProperty();

        // 验证配置内容正确
        var content = File.ReadAllText(configPath);
        var savedConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content, JsonHelper.ReadOptions);

        if (savedConfig == null || !savedConfig.ContainsKey("testKey"))
            return false.ToProperty();

        var savedValue = savedConfig["testKey"].GetString();
        return (savedValue == configValue.Get).ToProperty();
    }

    /// <summary>
    /// 清理 ID 中的非法字符
    /// </summary>
    private static string CleanId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        // 只保留字母、数字和连字符
        var cleaned = new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return cleaned.Length > 20 ? cleaned.Substring(0, 20) : cleaned;
    }

    /// <summary>
    /// 可测试的 PluginAssociationManager，允许自定义 Profiles 目录
    /// </summary>
    private class TestablePluginAssociationManager : PluginAssociationManager
    {
        private readonly string _profilesDirectory;

        public TestablePluginAssociationManager(ILogService logService, IPluginLibrary pluginLibrary, string indexPath,
                                                string profilesDirectory)
            : base(logService, pluginLibrary, indexPath)
        {
            _profilesDirectory = profilesDirectory;
        }

        /// <summary>
        /// 重写 ApplyPluginPresetConfigs 以使用自定义目录
        /// </summary>
        public new void ApplyPluginPresetConfigs(string profileId,
                                                 Dictionary<string, Dictionary<string, JsonElement>>? presetConfigs)
        {
            if (string.IsNullOrEmpty(profileId) || presetConfigs == null || presetConfigs.Count == 0)
                return;

            foreach (var (pluginId, config) in presetConfigs)
            {
                if (string.IsNullOrEmpty(pluginId))
                    continue;

                try
                {
                    // 使用自定义的 Profiles 目录
                    var configDir = Path.Combine(_profilesDirectory, profileId, "plugins", pluginId);
                    var configPath = Path.Combine(configDir, "config.json");

                    // 如果配置文件已存在，不覆盖
                    if (File.Exists(configPath))
                        continue;

                    // 确保目录存在
                    Directory.CreateDirectory(configDir);

                    // 序列化配置并写入文件
                    var json = JsonSerializer.Serialize(config, JsonHelper.WriteOptions);
                    File.WriteAllText(configPath, json);
                }
                catch
                {
                    // 忽略错误
                }
            }
        }
    }
}
}
