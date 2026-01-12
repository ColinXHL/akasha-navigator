using System;
using System.Collections.Generic;
using System.Text.Json;
using AkashaNavigator.Models.Config;
using Xunit;

namespace AkashaNavigator.Tests.Models.Config
{
/// <summary>
/// GlobalCursorDetectionConfig 单元测试
/// 测试全局鼠标检测配置的默认值、序列化和反序列化
/// </summary>
public class GlobalCursorDetectionConfigTests
{

#region Default Values Tests - 6.2.1

[Fact]
public void Constructor_WithNoParameters_HasCorrectDefaults()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.False(config.Enabled);                    // 默认禁用
Assert.Null(config.ProcessWhitelist);            // 默认无白名单
Assert.Equal(0.3, config.MinOpacity);            // 默认 30% 透明度
Assert.Equal(200, config.CheckIntervalMs);       // 默认 200ms 间隔
Assert.False(config.EnableDebugLog);             // 默认不调试
}

[Fact]
public void Enabled_DefaultValue_IsFalse()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.False(config.Enabled);
}

[Fact]
public void ProcessWhitelist_DefaultValue_IsNull()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.Null(config.ProcessWhitelist);
}

[Fact]
public void MinOpacity_DefaultValue_IsZeroPointThree()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.Equal(0.3, config.MinOpacity);
}

[Fact]
public void CheckIntervalMs_DefaultValue_IsTwoHundred()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.Equal(200, config.CheckIntervalMs);
}

[Fact]
public void EnableDebugLog_DefaultValue_IsFalse()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig();

// Assert
Assert.False(config.EnableDebugLog);
}

#endregion

#region Serialization Tests - 6.2.2

[Fact]
public void Serialize_WithAllValues_ProducesCorrectJson()
{
// Arrange
var config = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "eldenring", "genshinimpact" },
MinOpacity = 0.2,
CheckIntervalMs = 150,
EnableDebugLog = true
};

// Act
var json = JsonSerializer.Serialize(config);

// Assert
Assert.Contains("\"Enabled\":true", json);
Assert.Contains("\"MinOpacity\":0.2", json);
Assert.Contains("\"CheckIntervalMs\":150", json);
Assert.Contains("\"EnableDebugLog\":true", json);
Assert.Contains("eldenring", json);
Assert.Contains("genshinimpact", json);
}

[Fact]
public void Deserialize_WithValidJson_ProducesCorrectConfig()
{
// Arrange
var json = @"{
""Enabled"": true,
""ProcessWhitelist"": [""game1"", ""game2""],
""MinOpacity"": 0.4,
""CheckIntervalMs"": 300,
""EnableDebugLog"": true
}";

// Act
var config = JsonSerializer.Deserialize<GlobalCursorDetectionConfig>(json);

// Assert
Assert.NotNull(config);
Assert.True(config.Enabled);
Assert.NotNull(config.ProcessWhitelist);
Assert.Equal(2, config.ProcessWhitelist.Count);
Assert.Contains("game1", config.ProcessWhitelist);
Assert.Contains("game2", config.ProcessWhitelist);
Assert.Equal(0.4, config.MinOpacity);
Assert.Equal(300, config.CheckIntervalMs);
Assert.True(config.EnableDebugLog);
}

[Fact]
public void Deserialize_WithMissingValues_UsesDefaults()
{
// Arrange
var json = @"{ ""Enabled"": true }";

// Act
var config = JsonSerializer.Deserialize<GlobalCursorDetectionConfig>(json);

// Assert
Assert.NotNull(config);
Assert.True(config.Enabled);
Assert.Null(config.ProcessWhitelist);
Assert.Equal(0.3, config.MinOpacity);      // 默认值
Assert.Equal(200, config.CheckIntervalMs); // 默认值
Assert.False(config.EnableDebugLog);       // 默认值
}

[Fact]
public void Serialize_ThenDeserialize_PreservesAllProperties()
{
// Arrange
var original = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "eldenring", "starfield", "genshinimpact" },
MinOpacity = 0.25,
CheckIntervalMs = 175,
EnableDebugLog = true
};

// Act
var json = JsonSerializer.Serialize(original);
var deserialized = JsonSerializer.Deserialize<GlobalCursorDetectionConfig>(json);

// Assert
Assert.NotNull(deserialized);
Assert.Equal(original.Enabled, deserialized.Enabled);
Assert.NotNull(deserialized.ProcessWhitelist);
Assert.Equal(original.ProcessWhitelist.Count, deserialized.ProcessWhitelist.Count);
Assert.Equal(original.ProcessWhitelist[0], deserialized.ProcessWhitelist[0]);
Assert.Equal(original.ProcessWhitelist[1], deserialized.ProcessWhitelist[1]);
Assert.Equal(original.ProcessWhitelist[2], deserialized.ProcessWhitelist[2]);
Assert.Equal(original.MinOpacity, deserialized.MinOpacity);
Assert.Equal(original.CheckIntervalMs, deserialized.CheckIntervalMs);
Assert.Equal(original.EnableDebugLog, deserialized.EnableDebugLog);
}

[Fact]
public void Deserialize_WithEmptyJson_HasDefaultValues()
{
// Arrange
var json = "{}";

// Act
var config = JsonSerializer.Deserialize<GlobalCursorDetectionConfig>(json);

// Assert
Assert.NotNull(config);
Assert.False(config.Enabled);
Assert.Null(config.ProcessWhitelist);
Assert.Equal(0.3, config.MinOpacity);
Assert.Equal(200, config.CheckIntervalMs);
Assert.False(config.EnableDebugLog);
}

#endregion

#region ProcessWhitelist Tests - 6.2.3

[Fact]
public void ProcessWhitelist_WithEmptyList_AllowsEmpty()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string>()
};

// Assert
Assert.NotNull(config.ProcessWhitelist);
Assert.Empty(config.ProcessWhitelist);
}

[Fact]
public void ProcessWhitelist_WithSingleItem_WorksCorrectly()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string> { "eldenring" }
};

// Assert
Assert.NotNull(config.ProcessWhitelist);
Assert.Single(config.ProcessWhitelist);
Assert.Equal("eldenring", config.ProcessWhitelist[0]);
}

[Fact]
public void ProcessWhitelist_WithMultipleItems_WorksCorrectly()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string> { "game1", "game2", "game3" }
};

// Assert
Assert.NotNull(config.ProcessWhitelist);
Assert.Equal(3, config.ProcessWhitelist.Count);
}

[Fact]
public void ProcessWhitelist_CaseInsensitive_MatchWorks()
{
// Arrange
var config = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string> { "EldenRing", "GenshinImpact" }
};

// Act & Assert - 验证列表包含正确的值
Assert.Contains("EldenRing", config.ProcessWhitelist);
Assert.Contains("GenshinImpact", config.ProcessWhitelist);

// 注意：白名单匹配逻辑在 CursorDetectionConfigResolver 中测试
}

#endregion

#region Property Assignment Tests - 6.2.4

[Fact]
public void Enabled_CanBeSetToTrue()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { Enabled = true };

// Assert
Assert.True(config.Enabled);
}

[Fact]
public void MinOpacity_CanBeSetToVariousValues()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { MinOpacity = 0.5 };

// Assert
Assert.Equal(0.5, config.MinOpacity);

// Act - 更新为另一个值
config.MinOpacity = 0.8;

// Assert
Assert.Equal(0.8, config.MinOpacity);
}

[Fact]
public void CheckIntervalMs_CanBeSetToVariousValues()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { CheckIntervalMs = 100 };

// Assert
Assert.Equal(100, config.CheckIntervalMs);

// Act - 更新为另一个值
config.CheckIntervalMs = 500;

// Assert
Assert.Equal(500, config.CheckIntervalMs);
}

[Fact]
public void EnableDebugLog_CanBeSetToTrue()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { EnableDebugLog = true };

// Assert
Assert.True(config.EnableDebugLog);
}

#endregion

#region Edge Cases Tests - 6.2.5

[Fact]
public void MinOpacity_Zero_IsAllowed()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { MinOpacity = 0.0 };

// Assert
Assert.Equal(0.0, config.MinOpacity);
}

[Fact]
public void MinOpacity_One_IsAllowed()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { MinOpacity = 1.0 };

// Assert
Assert.Equal(1.0, config.MinOpacity);
}

[Fact]
public void CheckIntervalMs_WithValueOfFifty_IsAllowed()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig { CheckIntervalMs = 50 };

// Assert
Assert.Equal(50, config.CheckIntervalMs);
}

[Fact]
public void ProcessWhitelist_WithDuplicateEntries_AllowsDuplicates()
{
// Arrange & Act
var config = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string> { "game", "game", "game" }
};

// Assert
Assert.Equal(3, config.ProcessWhitelist.Count);
// 注意：去重逻辑在实际使用时处理
}

#endregion

}
}
