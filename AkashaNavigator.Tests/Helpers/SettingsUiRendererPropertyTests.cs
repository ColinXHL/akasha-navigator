using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using FsCheck;
using FsCheck.Xunit;

namespace AkashaNavigator.Tests.Helpers
{
/// <summary>
/// SettingsUiRenderer 属性测试
/// </summary>
public class SettingsUiRendererPropertyTests
{
    /// <summary>
    /// 清理进程名：移除逗号、控制字符等不合法字符
    /// </summary>
    private static string CleanProcessName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        var cleaned = new string(name.Where(c => c != ',' && !char.IsControl(c)).ToArray());
        return cleaned.Trim();
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 3: Config Value Loading**
    /// **Validates: Requirements 4.1, 4.3**
    ///
    /// *For any* settings definition and config with matching keys,
    /// the rendered control SHALL display the config value;
    /// if config value is missing, it SHALL display the default value from the definition.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_ConfigValue_ShouldBeLoadedCorrectly(NonEmptyString processName1,
                                                                    NonEmptyString processName2)
    {
        // 清理进程名（移除逗号、控制字符和空白）
        var p1 = CleanProcessName(processName1.Get);
        var p2 = CleanProcessName(processName2.Get);

        // 跳过无效输入
        if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2) || p1 == p2)
            return true.ToProperty();

        // 创建配置值
        var configValue = $"{p1}, {p2}";

        // 解析配置值
        var parsed = ProcessListHelper.ParseProcessList(configValue);

        // 验证解析结果包含两个进程
        var containsBoth = parsed.Contains(p1) && parsed.Contains(p2);

        return containsBoth.Label(
            $"Config: '{configValue}', Parsed: [{string.Join(", ", parsed)}], Expected: [{p1}, {p2}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 3: Config Value Loading**
    /// **Validates: Requirements 4.1, 4.3**
    ///
    /// *For any* settings definition with default value and empty config,
    /// the default value SHALL be used.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_DefaultValue_ShouldBeUsedWhenConfigEmpty(NonEmptyString defaultProcess)
    {
        // 清理进程名（移除逗号、控制字符和空白）
        var defaultValue = CleanProcessName(defaultProcess.Get);

        // 跳过无效输入
        if (string.IsNullOrEmpty(defaultValue))
            return true.ToProperty();

        // 创建配置（空）
        var config = new PluginConfig("test-plugin");

        // 创建设置项定义
        var item = new SettingsItem { Type = "processList", Key = "processWhitelist", Label = "进程列表",
                                      Default = System.Text.Json.JsonSerializer.SerializeToElement(defaultValue) };

        // 获取配置值（应该返回 null）
        var configValue = config.Get<string?>(item.Key, null);

        // 如果配置值为空，应该使用默认值
        var valueToUse = configValue ?? item.GetDefaultValue<string>() ?? string.Empty;

        // 解析值
        var parsed = ProcessListHelper.ParseProcessList(valueToUse);

        // 验证默认值被正确使用
        var usesDefault = parsed.Contains(defaultValue);

        return usesDefault.Label(
            $"Default: '{defaultValue}', ConfigValue: '{configValue}', ValueToUse: '{valueToUse}', Parsed: [{string.Join(", ", parsed)}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 3: Config Value Loading**
    /// **Validates: Requirements 4.1, 4.3**
    ///
    /// *For any* config value, it SHALL override the default value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_ConfigValue_ShouldOverrideDefault(NonEmptyString configProcess,
                                                                  NonEmptyString defaultProcess)
    {
        // 清理进程名（移除逗号、控制字符和空白）
        var configValue = CleanProcessName(configProcess.Get);
        var defaultValue = CleanProcessName(defaultProcess.Get);

        // 跳过无效输入
        if (string.IsNullOrEmpty(configValue) || string.IsNullOrEmpty(defaultValue) || configValue == defaultValue)
            return true.ToProperty();

        // 创建配置并设置值
        var config = new PluginConfig("test-plugin");
        config.Set("processWhitelist", configValue);

        // 创建设置项定义
        var item = new SettingsItem { Type = "processList", Key = "processWhitelist", Label = "进程列表",
                                      Default = System.Text.Json.JsonSerializer.SerializeToElement(defaultValue) };

        // 获取配置值
        var loadedValue = config.Get<string?>(item.Key, null);

        // 如果配置值存在，应该使用配置值而不是默认值
        var valueToUse = loadedValue ?? item.GetDefaultValue<string>() ?? string.Empty;

        // 解析值
        var parsed = ProcessListHelper.ParseProcessList(valueToUse);

        // 验证配置值覆盖了默认值
        var usesConfig = parsed.Contains(configValue) && !parsed.Contains(defaultValue);

        return usesConfig.Label(
            $"Config: '{configValue}', Default: '{defaultValue}', Loaded: '{loadedValue}', Parsed: [{string.Join(", ", parsed)}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* slider with min=0 and max<=1, when format is null or "percent",
    /// the displayed value SHALL be formatted as percentage (value * 100 + "%").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_AutoDetect_PercentFormat_WhenMinZeroMaxLessOrEqualOne(PositiveInt maxMultiplier)
    {
        // 生成 0 < max <= 1 的值
        var max = Math.Min(1.0, maxMultiplier.Get / 100.0);
        if (max <= 0)
            max = 0.1;

        var item = new SettingsItem {
            Type = "slider", Key = "test", Min = 0, Max = max,
            Format = null // 自动检测
        };

        var format = SettingsUiRenderer.DetermineSliderFormat(item);

        return (format == "percent").Label($"Min: 0, Max: {max}, Expected: percent, Actual: {format}");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* slider with max > 1, when format is null,
    /// the displayed value SHALL be formatted as integer.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_AutoDetect_IntegerFormat_WhenMaxGreaterThanOne(PositiveInt maxValue)
    {
        // 确保 max > 1
        var max = maxValue.Get + 1.0;

        var item = new SettingsItem {
            Type = "slider", Key = "test", Min = 0, Max = max,
            Format = null // 自动检测
        };

        var format = SettingsUiRenderer.DetermineSliderFormat(item);

        return (format == "integer").Label($"Min: 0, Max: {max}, Expected: integer, Actual: {format}");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* slider with explicit format, the specified format SHALL be used.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_ExplicitFormat_ShouldOverrideAutoDetect(PositiveInt maxValue)
    {
        var formats = new[] { "percent", "integer", "decimal" };
        var max = maxValue.Get + 1.0; // max > 1, 自动检测会选择 integer

        var results = new List<bool>();
        foreach (var expectedFormat in formats)
        {
            var item = new SettingsItem { Type = "slider", Key = "test", Min = 0, Max = max, Format = expectedFormat };

            var actualFormat = SettingsUiRenderer.DetermineSliderFormat(item);
            results.Add(actualFormat == expectedFormat);
        }

        return results.All(r => r).Label($"Max: {max}, All explicit formats should be respected");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* value in [0, 1], percent format should display as "X%"
    /// where X is value * 100 rounded to integer.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_PercentFormat_ShouldDisplayCorrectly(PositiveInt valueMultiplier)
    {
        // 生成 0 <= value <= 1 的值
        var value = Math.Min(1.0, valueMultiplier.Get / 100.0);

        var formatted = SettingsUiRenderer.FormatSliderValue(value, "percent");
        var expected = $"{(value * 100):F0}%";

        return (formatted == expected).Label($"Value: {value}, Expected: {expected}, Actual: {formatted}");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* value, integer format should display as rounded integer.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_IntegerFormat_ShouldDisplayRoundedValue(NormalFloat value)
    {
        var v = value.Get;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return true.ToProperty();

        var formatted = SettingsUiRenderer.FormatSliderValue(v, "integer");
        var expected = v.ToString("F0");

        return (formatted == expected).Label($"Value: {v}, Expected: {expected}, Actual: {formatted}");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* value, decimal format should display with one decimal place.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_DecimalFormat_ShouldDisplayOneDecimalPlace(NormalFloat value)
    {
        var v = value.Get;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return true.ToProperty();

        var formatted = SettingsUiRenderer.FormatSliderValue(v, "decimal");
        var expected = v.ToString("F1");

        return (formatted == expected).Label($"Value: {v}, Expected: {expected}, Actual: {formatted}");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 2: Slider Percentage Format Detection**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// *For any* unknown format, should default to integer format.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Slider_UnknownFormat_ShouldDefaultToInteger(NonEmptyString unknownFormat, NormalFloat value)
    {
        var format = unknownFormat.Get;
        // 排除已知格式
        if (format == "percent" || format == "integer" || format == "decimal")
            return true.ToProperty();

        var v = value.Get;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return true.ToProperty();

        var formatted = SettingsUiRenderer.FormatSliderValue(v, format);
        var expected = v.ToString("F0"); // 默认整数格式

        return (formatted == expected)
            .Label($"Format: {format}, Value: {v}, Expected: {expected}, Actual: {formatted}");
    }
}
}
