using System;
using System.IO;
using System.Text.Json;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Profile;
using Xunit;

namespace AkashaNavigator.Tests.Models
{
/// <summary>
/// GameProfile 单元测试
/// 测试 Profile 模型验证、序列化/反序列化和默认值
/// </summary>
public class GameProfileTests
{
#region Profile Model Validation Tests - 3.2.1

    [Fact]
    public void Constructor_WithNoParameters_HasDefaultValues()
    {
        // Arrange & Act
        var profile = new GameProfile();

        // Assert
        Assert.Equal("default", profile.Id);
        Assert.Equal("Default", profile.Name);
        Assert.Equal("🌐", profile.Icon);
        Assert.Equal(1, profile.Version);
        Assert.Null(profile.Activation);
        Assert.Null(profile.Defaults);
        Assert.Null(profile.QuickLinks);
        Assert.Null(profile.Tools);
        Assert.Null(profile.CustomScript);
        Assert.Null(profile.PluginConfigs);
    }

    [Fact]
    public void Constructor_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var profile = new GameProfile { Id = "custom_profile", Name = "Custom Profile", Icon = "🎮", Version = 2,
                                        CustomScript = "custom.js" };

        // Assert
        Assert.Equal("custom_profile", profile.Id);
        Assert.Equal("Custom Profile", profile.Name);
        Assert.Equal("🎮", profile.Icon);
        Assert.Equal(2, profile.Version);
        Assert.Equal("custom.js", profile.CustomScript);
    }

    [Fact]
    public void ProfileActivation_WithDefaultValues_AutoSwitchEnabled()
    {
        // Arrange & Act
        var activation = new ProfileActivation();

        // Assert
        Assert.True(activation.AutoSwitch);
        Assert.Null(activation.Processes);
    }

    [Fact]
    public void ProfileActivation_WithCustomProcesses_StoresCorrectly()
    {
        // Arrange & Act
        var activation =
            new ProfileActivation { AutoSwitch = false, Processes = new List<string> { "game.exe", "launcher.exe" } };

        // Assert
        Assert.False(activation.AutoSwitch);
        Assert.NotNull(activation.Processes);
        Assert.Equal(2, activation.Processes.Count);
        Assert.Contains("game.exe", activation.Processes);
        Assert.Contains("launcher.exe", activation.Processes);
    }

    [Fact]
    public void ProfileDefaults_WithDefaultValues_HasCorrectDefaults()
    {
        // Arrange & Act
        var defaults = new ProfileDefaults();

        // Assert
        Assert.Null(defaults.Url);
        Assert.Equal(5, defaults.SeekSeconds);
    }

    [Fact]
    public void ProfileDefaults_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var defaults = new ProfileDefaults { Url = "https://example.com", SeekSeconds = 10 };

        // Assert
        Assert.Equal("https://example.com", defaults.Url);
        Assert.Equal(10, defaults.SeekSeconds);
    }

    [Fact]
    public void QuickLink_WithDefaultValues_HasEmptyDefaults()
    {
        // Arrange & Act
        var link = new QuickLink();

        // Assert
        Assert.Equal(string.Empty, link.Label);
        Assert.Null(link.Url);
        Assert.Null(link.Type);
        Assert.Null(link.Path);
        Assert.False(link.Separator);
    }

    [Fact]
    public void QuickLink_Separator_HasOnlySeparatorFlag()
    {
        // Arrange & Act
        var link = new QuickLink { Separator = true };

        // Assert
        Assert.True(link.Separator);
        Assert.Equal(string.Empty, link.Label);
    }

    [Fact]
    public void QuickLink_WithUrl_StoresCorrectly()
    {
        // Arrange & Act
        var link = new QuickLink { Label = "YouTube", Url = "https://youtube.com", Type = "url" };

        // Assert
        Assert.Equal("YouTube", link.Label);
        Assert.Equal("https://youtube.com", link.Url);
        Assert.Equal("url", link.Type);
    }

    [Fact]
    public void ExternalTool_WithDefaultValues_HasEmptyDefaults()
    {
        // Arrange & Act
        var tool = new ExternalTool();

        // Assert
        Assert.Equal(string.Empty, tool.Label);
        Assert.Equal(string.Empty, tool.Path);
        Assert.Null(tool.Args);
        Assert.False(tool.RunAsAdmin);
    }

    [Fact]
    public void ExternalTool_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var tool = new ExternalTool { Label = "Notepad", Path = "C:\\Windows\\System32\\notepad.exe",
                                      Args = "--new-window", RunAsAdmin = true };

        // Assert
        Assert.Equal("Notepad", tool.Label);
        Assert.Equal("C:\\Windows\\System32\\notepad.exe", tool.Path);
        Assert.Equal("--new-window", tool.Args);
        Assert.True(tool.RunAsAdmin);
    }

#endregion

#region Serialization / Deserialization Tests - 3.2.2

    [Fact]
    public void Serialize_WithCompleteProfile_ProducesValidJson()
    {
        // Arrange
        var profile = new GameProfile {
            Id = "test_profile",
            Name = "Test Profile",
            Icon = "🧪",
            Version = 1,
            Activation = new ProfileActivation { AutoSwitch = true, Processes = new List<string> { "test.exe" } },
            Defaults = new ProfileDefaults { Url = "https://test.com", SeekSeconds = 15 },
            QuickLinks =
                new List<QuickLink> { new QuickLink { Label = "Test", Url = "https://test.com", Type = "url" } },
            Tools = new List<ExternalTool> { new ExternalTool { Label = "Tool", Path = "C:\\tool.exe" } },
            CustomScript = "test.js"
        };

        // Act
        var json = JsonSerializer.Serialize(profile, JsonHelper.WriteOptions);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("test_profile", json);
        Assert.Contains("Test Profile", json);
        Assert.Contains("test.exe", json);
    }

    [Fact]
    public void Deserialize_WithValidJson_ReturnsCorrectProfile()
    {
        // Arrange
        var json = @"{
""id"": ""deserialize_test"",
""name"": ""Deserialize Test"",
""icon"": ""📋"",
""version"": 2,
""activation"": {
    ""autoSwitch"": false,
    ""processes"": [""app.exe""]
},
""defaults"": {
    ""url"": ""https://example.com"",
    ""seekSeconds"": 20
},
""quickLinks"": [
    { ""label"": ""Example"", ""url"": ""https://example.com"", ""type"": ""url"" }
],
""tools"": [
    { ""label"": ""Editor"", ""path"": ""C:\\editor.exe"" }
],
""customScript"": ""custom.js""
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("deserialize_test", profile.Id);
        Assert.Equal("Deserialize Test", profile.Name);
        Assert.Equal("📋", profile.Icon);
        Assert.Equal(2, profile.Version);
        Assert.NotNull(profile.Activation);
        Assert.False(profile.Activation.AutoSwitch);
        Assert.NotNull(profile.Activation.Processes);
        Assert.Single(profile.Activation.Processes);
        Assert.Equal("app.exe", profile.Activation.Processes[0]);
        Assert.NotNull(profile.Defaults);
        Assert.Equal("https://example.com", profile.Defaults.Url);
        Assert.Equal(20, profile.Defaults.SeekSeconds);
        Assert.NotNull(profile.QuickLinks);
        Assert.Single(profile.QuickLinks);
        Assert.Equal("Example", profile.QuickLinks[0].Label);
        Assert.NotNull(profile.Tools);
        Assert.Single(profile.Tools);
        Assert.Equal("Editor", profile.Tools[0].Label);
        Assert.Equal("custom.js", profile.CustomScript);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesAllProperties()
    {
        // Arrange
        var original = new GameProfile {
            Id = "roundtrip_test",
            Name = "Roundtrip Test",
            Icon = "🔄",
            Version = 3,
            Activation =
                new ProfileActivation { AutoSwitch = true, Processes = new List<string> { "game1.exe", "game2.exe" } },
            Defaults = new ProfileDefaults { Url = "https://roundtrip.com", SeekSeconds = 12 },
            QuickLinks =
                new List<QuickLink> { new QuickLink { Label = "Link1", Url = "https://link1.com", Type = "url" },
                                      new QuickLink { Label = "Link2", Url = "https://link2.com", Type = "url" } },
            Tools = new List<ExternalTool> { new ExternalTool { Label = "Tool1", Path = "C:\\tool1.exe",
                                                                Args = "--arg1", RunAsAdmin = false },
                                             new ExternalTool { Label = "Tool2", Path = "C:\\tool2.exe",
                                                                Args = "--arg2", RunAsAdmin = true } },
            CustomScript = "roundtrip.js"
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonHelper.WriteOptions);
        var deserialized = JsonSerializer.Deserialize<GameProfile>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Icon, deserialized.Icon);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.NotNull(deserialized.Activation);
        Assert.Equal(original.Activation.AutoSwitch, deserialized.Activation.AutoSwitch);
        Assert.NotNull(deserialized.Activation.Processes);
        Assert.Equal(original.Activation.Processes.Count, deserialized.Activation.Processes.Count);
        Assert.Equal(original.Activation.Processes[0], deserialized.Activation.Processes[0]);
        Assert.Equal(original.Activation.Processes[1], deserialized.Activation.Processes[1]);
        Assert.NotNull(deserialized.Defaults);
        Assert.Equal(original.Defaults.Url, deserialized.Defaults.Url);
        Assert.Equal(original.Defaults.SeekSeconds, deserialized.Defaults.SeekSeconds);
        Assert.NotNull(deserialized.QuickLinks);
        Assert.Equal(original.QuickLinks.Count, deserialized.QuickLinks.Count);
        Assert.Equal(original.QuickLinks[0].Label, deserialized.QuickLinks[0].Label);
        Assert.Equal(original.QuickLinks[0].Url, deserialized.QuickLinks[0].Url);
        Assert.NotNull(deserialized.Tools);
        Assert.Equal(original.Tools.Count, deserialized.Tools.Count);
        Assert.Equal(original.Tools[0].Label, deserialized.Tools[0].Label);
        Assert.Equal(original.Tools[0].Path, deserialized.Tools[0].Path);
        Assert.Equal(original.Tools[0].Args, deserialized.Tools[0].Args);
        Assert.Equal(original.Tools[0].RunAsAdmin, deserialized.Tools[0].RunAsAdmin);
        Assert.Equal(original.Tools[1].RunAsAdmin, deserialized.Tools[1].RunAsAdmin);
        Assert.Equal(original.CustomScript, deserialized.CustomScript);
    }

    [Fact]
    public void Deserialize_WithMinimalJson_HasDefaultValuesForMissingProperties()
    {
        // Arrange
        var minimalJson = @"{
""id"": ""minimal"",
""name"": ""Minimal Profile""
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(minimalJson, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("minimal", profile.Id);
        Assert.Equal("Minimal Profile", profile.Name);
        // 缺失的属性应使用默认值
        Assert.Equal("🌐", profile.Icon);
        Assert.Equal(1, profile.Version);
        Assert.Null(profile.Activation);
        Assert.Null(profile.Defaults);
        Assert.Null(profile.QuickLinks);
        Assert.Null(profile.Tools);
        Assert.Null(profile.CustomScript);
    }

    [Fact]
    public void Deserialize_WithPascalCaseProperties_WorksCorrectly()
    {
        // Arrange - 使用 PascalCase 属性名（旧格式兼容性）
        var pascalCaseJson = @"{
""Id"": ""pascal_test"",
""Name"": ""Pascal Test"",
""Icon"": ""🔤"",
""Version"": 1
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(pascalCaseJson, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("pascal_test", profile.Id);
        Assert.Equal("Pascal Test", profile.Name);
        Assert.Equal("🔤", profile.Icon);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public void Deserialize_WithEmptyCollections_HasEmptyCollections()
    {
        // Arrange
        var json = @"{
""id"": ""empty_collections"",
""name"": ""Empty Collections"",
""activation"": { ""processes"": [] },
""quickLinks"": [],
""tools"": []
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.NotNull(profile.Activation);
        Assert.NotNull(profile.Activation.Processes);
        Assert.Empty(profile.Activation.Processes);
        Assert.NotNull(profile.QuickLinks);
        Assert.Empty(profile.QuickLinks);
        Assert.NotNull(profile.Tools);
        Assert.Empty(profile.Tools);
    }

    [Fact]
    public void Deserialize_WithNullCollections_HasNullCollections()
    {
        // Arrange
        var json = @"{
""id"": ""null_collections"",
""name"": ""Null Collections"",
""activation"": { ""processes"": null },
""quickLinks"": null,
""tools"": null
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.NotNull(profile.Activation);
        Assert.Null(profile.Activation.Processes);
        Assert.Null(profile.QuickLinks);
        Assert.Null(profile.Tools);
    }

#endregion

#region Default Values Tests - 3.2.3

    [Fact]
    public void GameProfile_DefaultId_IsDefault()
    {
        // Act
        var profile = new GameProfile();

        // Assert
        Assert.Equal("default", profile.Id);
    }

    [Fact]
    public void GameProfile_DefaultName_IsDefault()
    {
        // Act
        var profile = new GameProfile();

        // Assert
        Assert.Equal("Default", profile.Name);
    }

    [Fact]
    public void GameProfile_DefaultIcon_IsGlobeEmoji()
    {
        // Act
        var profile = new GameProfile();

        // Assert
        Assert.Equal("🌐", profile.Icon);
    }

    [Fact]
    public void GameProfile_DefaultVersion_IsOne()
    {
        // Act
        var profile = new GameProfile();

        // Assert
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public void ProfileActivation_DefaultAutoSwitch_IsTrue()
    {
        // Act
        var activation = new ProfileActivation();

        // Assert
        Assert.True(activation.AutoSwitch);
    }

    [Fact]
    public void ProfileActivation_DefaultProcesses_IsNull()
    {
        // Act
        var activation = new ProfileActivation();

        // Assert
        Assert.Null(activation.Processes);
    }

    [Fact]
    public void ProfileDefaults_DefaultUrl_IsNull()
    {
        // Act
        var defaults = new ProfileDefaults();

        // Assert
        Assert.Null(defaults.Url);
    }

    [Fact]
    public void ProfileDefaults_DefaultSeekSeconds_IsFive()
    {
        // Act
        var defaults = new ProfileDefaults();

        // Assert
        Assert.Equal(5, defaults.SeekSeconds);
    }

    [Fact]
    public void QuickLink_DefaultLabel_IsEmptyString()
    {
        // Act
        var link = new QuickLink();

        // Assert
        Assert.Equal(string.Empty, link.Label);
    }

    [Fact]
    public void QuickLink_DefaultSeparator_IsFalse()
    {
        // Act
        var link = new QuickLink();

        // Assert
        Assert.False(link.Separator);
    }

    [Fact]
    public void ExternalTool_DefaultLabel_IsEmptyString()
    {
        // Act
        var tool = new ExternalTool();

        // Assert
        Assert.Equal(string.Empty, tool.Label);
    }

    [Fact]
    public void ExternalTool_DefaultPath_IsEmptyString()
    {
        // Act
        var tool = new ExternalTool();

        // Assert
        Assert.Equal(string.Empty, tool.Path);
    }

    [Fact]
    public void ExternalTool_DefaultRunAsAdmin_IsFalse()
    {
        // Act
        var tool = new ExternalTool();

        // Assert
        Assert.False(tool.RunAsAdmin);
    }

#endregion

#region PluginConfigs Tests

    [Fact]
    public void GameProfile_PluginConfigs_WithDefaults_IsNull()
    {
        // Arrange & Act
        var profile = new GameProfile();

        // Assert
        Assert.Null(profile.PluginConfigs);
    }

    [Fact]
    public void GameProfile_PluginConfigs_WithCustomValues_StoresCorrectly()
    {
        // Arrange
        var pluginConfigs = new Dictionary<string, Dictionary<string, JsonElement>> {
            ["test-plugin"] =
                new Dictionary<string, JsonElement> { ["setting1"] = JsonDocument.Parse("\"value1\"").RootElement,
                                                      ["setting2"] = JsonDocument.Parse("123").RootElement }
        };

        // Act
        var profile = new GameProfile { PluginConfigs = pluginConfigs };

        // Assert
        Assert.NotNull(profile.PluginConfigs);
        Assert.Single(profile.PluginConfigs);
        Assert.True(profile.PluginConfigs.ContainsKey("test-plugin"));
    }

    [Fact]
    public void GameProfile_Serialize_WithPluginConfigs_ProducesCorrectJson()
    {
        // Arrange
        var pluginConfigs = new Dictionary<string, Dictionary<string, JsonElement>> {
            ["smart-cursor-detection"] =
                new Dictionary<string, JsonElement> { ["processWhitelist"] =
                                                          JsonDocument.Parse("\"YuanShen, GenshinImpact\"")
                                                              .RootElement }
        };

        var profile = new GameProfile { Id = "test", Name = "Test", PluginConfigs = pluginConfigs };

        // Act
        var json = JsonSerializer.Serialize(profile, JsonHelper.WriteOptions);

        // Assert
        Assert.Contains("pluginConfigs", json);
        Assert.Contains("smart-cursor-detection", json);
        Assert.Contains("processWhitelist", json);
    }

    [Fact]
    public void GameProfile_Deserialize_WithPluginConfigs_ProducesCorrectConfig()
    {
        // Arrange
        var json = @"{
""id"": ""plugin_config_test"",
""name"": ""Plugin Config Test"",
""pluginConfigs"": {
    ""test-plugin"": {
        ""enabled"": true,
        ""threshold"": 0.5
    }
}
}";

        // Act
        var profile = JsonSerializer.Deserialize<GameProfile>(json, JsonHelper.ReadOptions);

        // Assert
        Assert.NotNull(profile);
        Assert.NotNull(profile.PluginConfigs);
        Assert.True(profile.PluginConfigs.ContainsKey("test-plugin"));
        var pluginConfig = profile.PluginConfigs["test-plugin"];
        Assert.True(pluginConfig.ContainsKey("enabled"));
        Assert.True(pluginConfig.ContainsKey("threshold"));
    }

#endregion
}
}
