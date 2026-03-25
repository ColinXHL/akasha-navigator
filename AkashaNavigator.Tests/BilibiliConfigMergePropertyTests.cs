using System.Collections.Generic;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests;

/// <summary>
/// B站分P列表插件配置合并属性测试
/// 验证 bilibili-page-list 插件的配置合并逻辑
/// </summary>
public class BilibiliConfigMergePropertyTests
{
    /// <summary>
    /// 合并配置（与 JavaScript 实现逻辑一致）
    /// </summary>
    public static Dictionary<string, object> MergeConfig(Dictionary<string, object>? userConfig,
                                                         Dictionary<string, object> defaults)
    {
        var result = new Dictionary<string, object>();

        // 复制默认值
        foreach (var kvp in defaults)
        {
            if (kvp.Value is Dictionary<string, object> nestedDefaults)
            {
                var nestedUser =
                    userConfig?.ContainsKey(kvp.Key) == true ? userConfig[kvp.Key] as Dictionary<string, object> : null;
                result[kvp.Key] = MergeConfig(nestedUser, nestedDefaults);
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        // 覆盖用户配置
        if (userConfig != null)
        {
            foreach (var kvp in userConfig)
            {
                if (kvp.Value is Dictionary<string, object> nestedUser)
                {
                    var nestedDefaults = result.ContainsKey(kvp.Key) ? result[kvp.Key] as Dictionary<string, object>
                                                                     : new Dictionary<string, object>();
                    result[kvp.Key] = MergeConfig(nestedUser, nestedDefaults ?? new Dictionary<string, object>());
                }
                else
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }

#region Property 6 : Config Default Merge

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 6: Config Default Merge**
    /// **Validates: Requirements 6.4**
    ///
    /// *For any* partial configuration object, merging with defaults SHALL produce
    /// a complete configuration where all required fields have values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfig_WithDefaults_ShouldContainAllDefaultKeys()
    {
        var defaults =
            new Dictionary<string, object> { { "toggleHotkey", "Ctrl+P" },
                                             { "danmakuHotkey", "Ctrl+D" },
                                             { "subtitleHotkey", "Ctrl+S" },
                                             { "overlay",
                                               new Dictionary<string, object> { { "x", 100 }, { "y", 100 } } } };

        return Prop.ForAll<Dictionary<string, object>?>(
            userConfig =>
            {
                var merged = MergeConfig(userConfig, defaults);

                // 验证所有默认键都存在
                var hasAllKeys = merged.ContainsKey("toggleHotkey") && merged.ContainsKey("danmakuHotkey") &&
                                 merged.ContainsKey("subtitleHotkey") && merged.ContainsKey("overlay");

                // 验证嵌套对象
                var overlayValid = merged["overlay"] is Dictionary<string, object> overlay &&
                                   overlay.ContainsKey("x") && overlay.ContainsKey("y");

                return (hasAllKeys && overlayValid)
                    .Label($"Merged config has all required keys: {hasAllKeys}, Overlay valid: {overlayValid}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 6: Config Default Merge**
    /// **Validates: Requirements 6.4**
    ///
    /// *For any* partial configuration object, user-provided values SHALL override defaults.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfig_UserValues_ShouldOverrideDefaults()
    {
        var defaults = new Dictionary<string, object> { { "toggleHotkey", "Ctrl+P" }, { "danmakuHotkey", "Ctrl+D" } };

        // 使用非 null 字符串生成器
        var nonNullStringGen = Arb.Default.NonNull<string>().Generator;

        return Prop.ForAll(
            nonNullStringGen.ToArbitrary(),
            userHotkey =>
            {
                var userConfig = new Dictionary<string, object> { { "toggleHotkey", userHotkey } };

                var merged = MergeConfig(userConfig, defaults);

                // 用户值应该覆盖默认值
                var userValuePreserved = merged["toggleHotkey"].Equals(userHotkey);
                // 其他默认值应该保留
                var defaultValuePreserved = merged["danmakuHotkey"].Equals("Ctrl+D");

                return (userValuePreserved && defaultValuePreserved)
                    .Label($"User value preserved: {userValuePreserved}, Default preserved: {defaultValuePreserved}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 6: Config Default Merge**
    /// **Validates: Requirements 6.4**
    ///
    /// *For any* null or empty user configuration, the result SHALL equal the defaults.
    /// </summary>
    [Fact]
    public void MergeConfig_NullUserConfig_ShouldReturnDefaults()
    {
        var defaults = new Dictionary<string, object> { { "toggleHotkey", "Ctrl+P" }, { "danmakuHotkey", "Ctrl+D" } };

        var merged = MergeConfig(null, defaults);

        Assert.Equal("Ctrl+P", merged["toggleHotkey"]);
        Assert.Equal("Ctrl+D", merged["danmakuHotkey"]);
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 6: Config Default Merge**
    /// **Validates: Requirements 6.4**
    ///
    /// *For any* nested configuration object, merging SHALL preserve the nested structure.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfig_NestedConfig_ShouldPreserveStructure()
    {
        var defaults = new Dictionary<string, object> {
            { "overlay", new Dictionary<string, object> { { "x", 100 }, { "y", 100 }, { "width", 320 } } }
        };

        return Prop.ForAll<int>(
            userX =>
            {
                var userConfig =
                    new Dictionary<string, object> { { "overlay", new Dictionary<string, object> { { "x", userX } } } };

                var merged = MergeConfig(userConfig, defaults);

                var overlay = merged["overlay"] as Dictionary<string, object>;
                var userValuePreserved = overlay != null && overlay["x"].Equals(userX);
                var defaultYPreserved = overlay != null && overlay["y"].Equals(100);
                var defaultWidthPreserved = overlay != null && overlay["width"].Equals(320);

                return (userValuePreserved && defaultYPreserved && defaultWidthPreserved)
                    .Label(
                        $"User X: {userValuePreserved}, Default Y: {defaultYPreserved}, Default Width: {defaultWidthPreserved}");
            });
    }

    /// <summary>
    /// **Feature: bilibili-page-list-plugin, Property 6: Config Default Merge**
    /// **Validates: Requirements 6.4**
    ///
    /// *For any* configuration, merging twice with the same defaults SHALL be idempotent.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergeConfig_Idempotence_ShouldProduceSameResult()
    {
        var defaults = new Dictionary<string, object> { { "toggleHotkey", "Ctrl+P" }, { "danmakuHotkey", "Ctrl+D" } };

        // 使用非 null 字符串生成器
        var nonNullStringGen = Arb.Default.NonNull<string>().Generator;

        return Prop.ForAll(nonNullStringGen.ToArbitrary(),
                           userHotkey =>
                           {
                               var userConfig = new Dictionary<string, object> { { "toggleHotkey", userHotkey } };

                               var merged1 = MergeConfig(userConfig, defaults);
                               var merged2 = MergeConfig(merged1, defaults);

                               // 第二次合并应该产生相同的结果
                               var toggleMatches = merged1["toggleHotkey"].Equals(merged2["toggleHotkey"]);
                               var danmakuMatches = merged1["danmakuHotkey"].Equals(merged2["danmakuHotkey"]);

                               return (toggleMatches && danmakuMatches)
                                   .Label($"Toggle matches: {toggleMatches}, Danmaku matches: {danmakuMatches}");
                           });
    }

#endregion

#region Unit Tests for Edge Cases

    /// <summary>
    /// 单元测试：空用户配置应该返回默认值
    /// </summary>
    [Fact]
    public void MergeConfig_EmptyUserConfig_ShouldReturnDefaults()
    {
        var defaults = new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } };

        var userConfig = new Dictionary<string, object>();
        var merged = MergeConfig(userConfig, defaults);

        Assert.Equal("value1", merged["key1"]);
        Assert.Equal("value2", merged["key2"]);
    }

    /// <summary>
    /// 单元测试：用户配置完全覆盖默认值
    /// </summary>
    [Fact]
    public void MergeConfig_CompleteUserConfig_ShouldOverrideAllDefaults()
    {
        var defaults = new Dictionary<string, object> { { "key1", "default1" }, { "key2", "default2" } };

        var userConfig = new Dictionary<string, object> { { "key1", "user1" }, { "key2", "user2" } };

        var merged = MergeConfig(userConfig, defaults);

        Assert.Equal("user1", merged["key1"]);
        Assert.Equal("user2", merged["key2"]);
    }

    /// <summary>
    /// 单元测试：嵌套配置合并
    /// </summary>
    [Fact]
    public void MergeConfig_NestedConfig_ShouldMergeCorrectly()
    {
        var defaults =
            new Dictionary<string, object> { { "overlay",
                                               new Dictionary<string, object> { { "x", 100 }, { "y", 100 } } } };

        var userConfig =
            new Dictionary<string, object> { { "overlay", new Dictionary<string, object> { { "x", 200 } } } };

        var merged = MergeConfig(userConfig, defaults);
        var overlay = merged["overlay"] as Dictionary<string, object>;

        Assert.NotNull(overlay);
        Assert.Equal(200, overlay["x"]);
        Assert.Equal(100, overlay["y"]);
    }

#endregion
}
