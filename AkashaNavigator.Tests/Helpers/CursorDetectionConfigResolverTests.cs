using System.Collections.Generic;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using Xunit;

namespace AkashaNavigator.Tests.Helpers
{
/// <summary>
/// CursorDetectionConfigResolver 单元测试
/// 测试全局配置与 Profile 配置的合并逻辑
/// </summary>
public class CursorDetectionConfigResolverTests
{

#region Profile Whitelist Priority Tests - 6.4.1

[Fact]
public void Resolve_ProfileWhitelistContainsProcess_UsesProfileConfig()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = false,
MinOpacity = 0.2,
CheckIntervalMs = 100,
EnableDebugLog = false,
ProcessWhitelist = new List<string> { "globalgame" }
};

var profileConfig = new CursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.6,
CheckIntervalMs = 250,
ProcessWhitelist = new List<string> { "eldenring" }
};

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "eldenring");

// Assert
Assert.True(enabled);       // Profile 配置优先
Assert.Equal(0.6, minOpacity);
Assert.Equal(250, intervalMs);
Assert.False(debugLog);
}

[Fact]
public void Resolve_GlobalWhitelistOnly_UsesGlobalConfig()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.3,
CheckIntervalMs = 200,
EnableDebugLog = true,
ProcessWhitelist = new List<string> { "globalgame" }
};

var profileConfig = new CursorDetectionConfig
{
ProcessWhitelist = null  // 空白名单，继承全局
};

// Act - 直接使用内联逻辑验证
string? foregroundProcess = "globalgame";

// 1. 检查 Profile 白名单（仅当 Profile 白名单非空时检查）
bool inProfileWhitelist = profileConfig?.ProcessWhitelist != null &&
profileConfig.ProcessWhitelist.Count > 0 &&
profileConfig.ProcessWhitelist.Any(p => p.Equals(foregroundProcess, StringComparison.OrdinalIgnoreCase));

// 2. 检查全局白名单（仅当全局白名单非空时检查）
bool inGlobalWhitelist = globalConfig?.ProcessWhitelist != null &&
globalConfig.ProcessWhitelist.Count > 0 &&
globalConfig.ProcessWhitelist.Any(p => p.Equals(foregroundProcess, StringComparison.OrdinalIgnoreCase));

// 3. 确定是否启用
bool expectedEnabled = false;
if (inProfileWhitelist)
{
expectedEnabled = profileConfig?.Enabled ?? globalConfig?.Enabled ?? false;
}
else if (inGlobalWhitelist)
{
expectedEnabled = globalConfig?.Enabled ?? false;
}

// 验证手动逻辑
Assert.False(inProfileWhitelist);
Assert.True(inGlobalWhitelist);
Assert.True(expectedEnabled);

// Act - 现在调用 Resolve 方法
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, foregroundProcess);

// Assert
Assert.Equal(expectedEnabled, enabled);  // 使用相等比较而不是直接 Assert.True
Assert.Equal(0.3, minOpacity);
Assert.Equal(200, intervalMs);
Assert.True(debugLog);
}

[Fact]
public void Resolve_NoWhitelistMatch_ReturnsDisabled()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "game1" }
};

var profileConfig = new CursorDetectionConfig
{
ProcessWhitelist = new List<string> { "game2" }
};

// Act
var (enabled, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game3");

// Assert
Assert.False(enabled);
}

#endregion

#region Null Handling Tests - 6.4.2

[Fact]
public void Resolve_NullGlobalConfig_UsesDefaults()
{
// Arrange
GlobalCursorDetectionConfig? globalConfig = null;
CursorDetectionConfig? profileConfig = null;

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "any");

// Assert
Assert.False(enabled);
Assert.Equal(0.3, minOpacity);     // 默认值
Assert.Equal(200, intervalMs);     // 默认值
Assert.False(debugLog);
}

[Fact]
public void Resolve_NullForegroundProcess_ReturnsDisabled()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (enabled, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, null, null);

// Assert
Assert.False(enabled);
}

[Fact]
public void Resolve_EmptyForegroundProcess_ReturnsDisabled()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (enabled, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, null, "");

// Assert
Assert.False(enabled);
}

#endregion

#region Config Merge Tests - 6.4.3

[Fact]
public void Resolve_ProfileInheritsGlobal_WhenProfileValuesAreNull()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.25,
CheckIntervalMs = 150,
EnableDebugLog = true,
ProcessWhitelist = new List<string> { "game" }
};

var profileConfig = new CursorDetectionConfig
{
Enabled = null,           // 继承全局
MinOpacity = null,        // 继承全局
CheckIntervalMs = null,   // 继承全局
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game");

// Assert
Assert.True(enabled);              // 全局 Enabled
Assert.Equal(0.25, minOpacity);    // 全局 MinOpacity
Assert.Equal(150, intervalMs);     // 全局 Interval
// 注意：EnableDebugLog 是 bool 而非 bool?，当 Profile 在白名单中时使用 Profile 的值
// Profile 未显式设置 EnableDebugLog，使用默认值 false
Assert.False(debugLog);            // Profile 的 EnableDebugLog（默认 false）
}

[Fact]
public void Resolve_ProfileOverridesGlobal_WhenProfileValuesAreSet()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
MinOpacity = 0.2,
CheckIntervalMs = 100,
EnableDebugLog = false,
ProcessWhitelist = new List<string> { "game" }
};

var profileConfig = new CursorDetectionConfig
{
MinOpacity = 0.7,        // 覆盖全局
CheckIntervalMs = 500,   // 覆盖全局
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (_, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game");

// Assert
Assert.Equal(0.7, minOpacity);     // Profile 值
Assert.Equal(500, intervalMs);     // Profile 值
Assert.False(debugLog);            // 全局值（Profile 未设置）
}

[Fact]
public void Resolve_OnlyProfileSet_UsesProfileValues()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = false,
MinOpacity = 0.2,
ProcessWhitelist = new List<string> { "game" }
};

var profileConfig = new CursorDetectionConfig
{
Enabled = true,            // 覆盖全局 Enabled
MinOpacity = 0.8,          // 覆盖全局
CheckIntervalMs = 300,     // 覆盖全局
EnableDebugLog = true,     // Profile 有值
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game");

// Assert
Assert.True(enabled);           // Profile Enabled
Assert.Equal(0.8, minOpacity);  // Profile MinOpacity
Assert.Equal(300, intervalMs);  // Profile CheckIntervalMs
Assert.True(debugLog);          // Profile EnableDebugLog
}

[Fact]
public void Resolve_ProfileNullGlobalEnabled_UsesGlobalValues()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.4,
CheckIntervalMs = 180,
EnableDebugLog = true,
ProcessWhitelist = new List<string> { "testgame" }
};

CursorDetectionConfig? profileConfig = null;

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "testgame");

// Assert
Assert.True(enabled);
Assert.Equal(0.4, minOpacity);
Assert.Equal(180, intervalMs);
Assert.True(debugLog);
}

#endregion

#region Case Insensitivity Tests - 6.4.4

[Fact]
public void Resolve_ProcessNameCaseInsensitive_MatchesCorrectly()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.3,
CheckIntervalMs = 200,
ProcessWhitelist = new List<string> { "EldenRing", "GenshinImpact" }
};

// Act
var (enabled1, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, null, "eldenring");
var (enabled2, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, null, "ELDENRING");
var (enabled3, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, null, "GeNsHiNiMpAcT");

// Assert
Assert.True(enabled1);
Assert.True(enabled2);
Assert.True(enabled3);
}

[Fact]
public void Resolve_ProfileWhitelistCaseInsensitive_MatchesCorrectly()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
ProcessWhitelist = new List<string> { "GlobalGame" }
};

var profileConfig = new CursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.5,
ProcessWhitelist = new List<string> { "ProfileGame" }
};

// Act
var (enabled, minOpacity, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "profilegame");

// Assert
Assert.True(enabled);
Assert.Equal(0.5, minOpacity);  // Profile 配置
}

#endregion

#region Debug Tests

[Fact]
public void Debug_GlobalWhitelistOnly_ShouldWork()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.3,
CheckIntervalMs = 200,
EnableDebugLog = true,
ProcessWhitelist = new List<string> { "globalgame" }
};

var profileConfig = new CursorDetectionConfig
{
ProcessWhitelist = null  // 空白名单，继承全局
};

// Act
var (enabled, minOpacity, intervalMs, debugLog) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "globalgame");

// Assert
Assert.True(enabled);
Assert.Equal(0.3, minOpacity);
Assert.Equal(200, intervalMs);
Assert.True(debugLog);
}

#endregion

#region Edge Cases Tests - 6.4.5

[Fact]
public void Resolve_BothWhitelistsEmpty_ReturnsDisabled()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string>()  // 空列表
};

var profileConfig = new CursorDetectionConfig
{
ProcessWhitelist = new List<string>()  // 空列表
};

// Act
var (enabled, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "anygame");

// Assert
Assert.False(enabled);
}

[Fact]
public void Resolve_ProfileDisabledExplicitly_DisablesEvenInWhitelist()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
ProcessWhitelist = new List<string> { "game" }
};

var profileConfig = new CursorDetectionConfig
{
Enabled = false,  // 明确禁用
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (enabled, _, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game");

// Assert
Assert.False(enabled);  // Profile 的 false 覆盖全局的 true
}

[Fact]
public void Resolve_SameProcessInBothWhitelists_ProfileTakesPriority()
{
// Arrange
var globalConfig = new GlobalCursorDetectionConfig
{
Enabled = true,
MinOpacity = 0.2,
ProcessWhitelist = new List<string> { "game" }
};

var profileConfig = new CursorDetectionConfig
{
MinOpacity = 0.9,
ProcessWhitelist = new List<string> { "game" }
};

// Act
var (_, minOpacity, _, _) = CursorDetectionConfigResolver.Resolve(
globalConfig, profileConfig, "game");

// Assert
Assert.Equal(0.9, minOpacity);  // Profile 值优先
}

#endregion

}
}
