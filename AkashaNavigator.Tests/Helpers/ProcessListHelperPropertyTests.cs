using AkashaNavigator.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace AkashaNavigator.Tests.Helpers
{
/// <summary>
/// ProcessListHelper 属性测试
/// </summary>
public class ProcessListHelperPropertyTests
{
    /// <summary>
    /// 清理进程名（与 ProcessListHelper 内部逻辑一致）
    /// </summary>
    private static string CleanProcessName(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return string.Empty;

        // 移除逗号、换行符、回车符和其他控制字符
        var cleaned =
            new string(processName.Where(c => c != ',' && c != '\n' && c != '\r' && !char.IsControl(c)).ToArray());

        return cleaned.Trim();
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// *For any* list of valid process names, serializing to comma-separated string
    /// and then parsing back SHALL produce an equivalent list (same elements, same order).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_RoundTrip_ShouldPreserveElements(List<string> processNames)
    {
        // 过滤掉 null 列表
        if (processNames == null)
            return true.ToProperty();

        // 清理输入：使用与 ProcessListHelper 相同的清理逻辑
        var cleanedInput = processNames.Where(s => !string.IsNullOrWhiteSpace(s))
                               .Select(CleanProcessName)
                               .Where(s => !string.IsNullOrEmpty(s))
                               .Distinct()
                               .ToList();

        // 序列化
        var serialized = ProcessListHelper.SerializeProcessList(cleanedInput);

        // 反序列化
        var parsed = ProcessListHelper.ParseProcessList(serialized);

        // 属性：往返后元素应该相同（顺序和内容）
        var sameCount = cleanedInput.Count == parsed.Count;
        var sameElements = cleanedInput.SequenceEqual(parsed);

        return (sameCount && sameElements)
            .Label($"Input: [{string.Join(", ", cleanedInput)}], " + $"Serialized: '{serialized}', " +
                   $"Parsed: [{string.Join(", ", parsed)}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// 验证从字符串开始的往返：解析后序列化应产生等价字符串
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_RoundTrip_FromString_ShouldBeIdempotent(NonEmptyString input)
    {
        var inputStr = input.Get;

        // 第一次解析
        var parsed1 = ProcessListHelper.ParseProcessList(inputStr);

        // 序列化
        var serialized = ProcessListHelper.SerializeProcessList(parsed1);

        // 第二次解析
        var parsed2 = ProcessListHelper.ParseProcessList(serialized);

        // 属性：两次解析结果应该相同
        var sameCount = parsed1.Count == parsed2.Count;
        var sameElements = parsed1.SequenceEqual(parsed2);

        return (sameCount && sameElements)
            .Label($"Input: '{inputStr}', " + $"Parsed1: [{string.Join(", ", parsed1)}], " +
                   $"Serialized: '{serialized}', " + $"Parsed2: [{string.Join(", ", parsed2)}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// 验证序列化是幂等的：序列化两次应产生相同结果
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_Serialize_ShouldBeIdempotent(List<string> processNames)
    {
        if (processNames == null)
            return true.ToProperty();

        // 第一次序列化
        var serialized1 = ProcessListHelper.SerializeProcessList(processNames);

        // 解析后再序列化
        var parsed = ProcessListHelper.ParseProcessList(serialized1);
        var serialized2 = ProcessListHelper.SerializeProcessList(parsed);

        // 属性：两次序列化结果应该相同
        return (serialized1 == serialized2).Label($"Serialized1: '{serialized1}', Serialized2: '{serialized2}'");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// 验证解析是幂等的：解析两次应产生相同结果
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_Parse_ShouldBeIdempotent(NonEmptyString input)
    {
        var inputStr = input.Get;

        // 第一次解析
        var parsed1 = ProcessListHelper.ParseProcessList(inputStr);

        // 序列化后再解析
        var serialized = ProcessListHelper.SerializeProcessList(parsed1);
        var parsed2 = ProcessListHelper.ParseProcessList(serialized);

        // 属性：两次解析结果应该相同
        var sameCount = parsed1.Count == parsed2.Count;
        var sameElements = parsed1.SequenceEqual(parsed2);

        return (sameCount && sameElements)
            .Label($"Parsed1: [{string.Join(", ", parsed1)}], Parsed2: [{string.Join(", ", parsed2)}]");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// 验证空白处理：带有空白的输入应该被正确清理
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_Parse_ShouldTrimWhitespace(string processName)
    {
        if (string.IsNullOrEmpty(processName))
            return true.ToProperty();

        // 添加前后空格
        var withSpaces = $"  {processName}  ";
        var parsed = ProcessListHelper.ParseProcessList(withSpaces);

        if (parsed.Count == 0)
            return true.ToProperty(); // 如果原始名称只有空白，解析后为空是正确的

        // 属性：解析后的进程名不应有前后空格
        var noLeadingSpace = !parsed[0].StartsWith(" ");
        var noTrailingSpace = !parsed[0].EndsWith(" ");

        return (noLeadingSpace && noTrailingSpace).Label($"Input: '{withSpaces}', Parsed: '{parsed[0]}'");
    }

    /// <summary>
    /// **Feature: plugin-settings-ui-enhancement, Property 1: Process List Round-Trip Serialization**
    /// **Validates: Requirements 1.9, 4.2**
    ///
    /// 验证去重：重复的进程名应该被移除
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessList_Parse_ShouldRemoveDuplicates(NonEmptyString processName)
    {
        // 清理进程名（移除控制字符、逗号等）
        var name = CleanProcessName(processName.Get);
        if (string.IsNullOrEmpty(name))
            return true.ToProperty(); // 如果清理后为空，跳过测试

        // 创建包含重复项的字符串
        var duplicated = $"{name}, {name}, {name}";
        var parsed = ProcessListHelper.ParseProcessList(duplicated);

        // 属性：解析后应该只有一个元素
        return (parsed.Count == 1 && parsed[0] == name).Label($"Input: '{duplicated}', Parsed count: {parsed.Count}");
    }
}
}
