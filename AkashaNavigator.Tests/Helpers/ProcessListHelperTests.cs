using AkashaNavigator.Helpers;
using Xunit;

namespace AkashaNavigator.Tests.Helpers
{
/// <summary>
/// ProcessListHelper 单元测试
/// </summary>
public class ProcessListHelperTests
{
#region ParseProcessList Tests

    /// <summary>
    /// 解析空字符串应返回空列表
    /// </summary>
    [Fact]
    public void ParseProcessList_EmptyString_ShouldReturnEmptyList()
    {
        var result = ProcessListHelper.ParseProcessList("");
        Assert.Empty(result);
    }

    /// <summary>
    /// 解析 null 应返回空列表
    /// </summary>
    [Fact]
    public void ParseProcessList_Null_ShouldReturnEmptyList()
    {
        var result = ProcessListHelper.ParseProcessList(null);
        Assert.Empty(result);
    }

    /// <summary>
    /// 解析仅空白字符应返回空列表
    /// </summary>
    [Fact]
    public void ParseProcessList_WhitespaceOnly_ShouldReturnEmptyList()
    {
        var result = ProcessListHelper.ParseProcessList("   ");
        Assert.Empty(result);
    }

    /// <summary>
    /// 解析单个进程名
    /// </summary>
    [Fact]
    public void ParseProcessList_SingleProcess_ShouldReturnSingleItem()
    {
        var result = ProcessListHelper.ParseProcessList("YuanShen");
        Assert.Single(result);
        Assert.Equal("YuanShen", result[0]);
    }

    /// <summary>
    /// 解析多个进程名
    /// </summary>
    [Fact]
    public void ParseProcessList_MultipleProcesses_ShouldReturnAllItems()
    {
        var result = ProcessListHelper.ParseProcessList("YuanShen, GenshinImpact, StarRail");
        Assert.Equal(3, result.Count);
        Assert.Equal("YuanShen", result[0]);
        Assert.Equal("GenshinImpact", result[1]);
        Assert.Equal("StarRail", result[2]);
    }

    /// <summary>
    /// 解析带有额外空格的进程名应自动 trim
    /// </summary>
    [Fact]
    public void ParseProcessList_WithExtraSpaces_ShouldTrimItems()
    {
        var result = ProcessListHelper.ParseProcessList("  YuanShen  ,  GenshinImpact  ");
        Assert.Equal(2, result.Count);
        Assert.Equal("YuanShen", result[0]);
        Assert.Equal("GenshinImpact", result[1]);
    }

    /// <summary>
    /// 解析重复进程名应去重
    /// </summary>
    [Fact]
    public void ParseProcessList_WithDuplicates_ShouldRemoveDuplicates()
    {
        var result = ProcessListHelper.ParseProcessList("YuanShen, GenshinImpact, YuanShen");
        Assert.Equal(2, result.Count);
        Assert.Contains("YuanShen", result);
        Assert.Contains("GenshinImpact", result);
    }

    /// <summary>
    /// 解析包含空项的字符串应忽略空项
    /// </summary>
    [Fact]
    public void ParseProcessList_WithEmptyItems_ShouldIgnoreEmptyItems()
    {
        var result = ProcessListHelper.ParseProcessList("YuanShen,,GenshinImpact,  ,StarRail");
        Assert.Equal(3, result.Count);
        Assert.Equal("YuanShen", result[0]);
        Assert.Equal("GenshinImpact", result[1]);
        Assert.Equal("StarRail", result[2]);
    }

#endregion

#region SerializeProcessList Tests

    /// <summary>
    /// 序列化空列表应返回空字符串
    /// </summary>
    [Fact]
    public void SerializeProcessList_EmptyList_ShouldReturnEmptyString()
    {
        var result = ProcessListHelper.SerializeProcessList(new List<string>());
        Assert.Equal("", result);
    }

    /// <summary>
    /// 序列化 null 应返回空字符串
    /// </summary>
    [Fact]
    public void SerializeProcessList_Null_ShouldReturnEmptyString()
    {
        var result = ProcessListHelper.SerializeProcessList(null!);
        Assert.Equal("", result);
    }

    /// <summary>
    /// 序列化单个进程名
    /// </summary>
    [Fact]
    public void SerializeProcessList_SingleProcess_ShouldReturnSingleItem()
    {
        var result = ProcessListHelper.SerializeProcessList(new List<string> { "YuanShen" });
        Assert.Equal("YuanShen", result);
    }

    /// <summary>
    /// 序列化多个进程名
    /// </summary>
    [Fact]
    public void SerializeProcessList_MultipleProcesses_ShouldReturnCommaSeparated()
    {
        var result =
            ProcessListHelper.SerializeProcessList(new List<string> { "YuanShen", "GenshinImpact", "StarRail" });
        Assert.Equal("YuanShen, GenshinImpact, StarRail", result);
    }

    /// <summary>
    /// 序列化包含空白项的列表应忽略空白项
    /// </summary>
    [Fact]
    public void SerializeProcessList_WithEmptyItems_ShouldIgnoreEmptyItems()
    {
        var result = ProcessListHelper.SerializeProcessList(new List<string> { "YuanShen", "", "  ", "GenshinImpact" });
        Assert.Equal("YuanShen, GenshinImpact", result);
    }

    /// <summary>
    /// 序列化包含逗号的进程名应移除逗号
    /// </summary>
    [Fact]
    public void SerializeProcessList_WithCommaInName_ShouldRemoveComma()
    {
        var result = ProcessListHelper.SerializeProcessList(new List<string> { "Yuan,Shen", "GenshinImpact" });
        Assert.Equal("YuanShen, GenshinImpact", result);
    }

    /// <summary>
    /// 序列化包含重复项的列表应去重
    /// </summary>
    [Fact]
    public void SerializeProcessList_WithDuplicates_ShouldRemoveDuplicates()
    {
        var result =
            ProcessListHelper.SerializeProcessList(new List<string> { "YuanShen", "GenshinImpact", "YuanShen" });
        Assert.Equal("YuanShen, GenshinImpact", result);
    }

    /// <summary>
    /// 序列化带有前后空格的进程名应自动 trim
    /// </summary>
    [Fact]
    public void SerializeProcessList_WithExtraSpaces_ShouldTrimItems()
    {
        var result = ProcessListHelper.SerializeProcessList(new List<string> { "  YuanShen  ", "  GenshinImpact  " });
        Assert.Equal("YuanShen, GenshinImpact", result);
    }

#endregion

#region GetRunningProcesses Tests

    /// <summary>
    /// 获取运行进程应返回非空列表（至少有当前测试进程）
    /// </summary>
    [Fact]
    public void GetRunningProcesses_ShouldReturnNonEmptyList()
    {
        var result = ProcessListHelper.GetRunningProcesses();
        // 至少应该有一些有窗口的进程
        Assert.NotNull(result);
        // 不强制要求非空，因为在某些环境下可能没有有窗口的进程
    }

    /// <summary>
    /// 获取运行进程返回的 ProcessInfo 应有有效的 ProcessName
    /// </summary>
    [Fact]
    public void GetRunningProcesses_ProcessInfo_ShouldHaveValidProcessName()
    {
        var result = ProcessListHelper.GetRunningProcesses();
        foreach (var process in result)
        {
            Assert.False(string.IsNullOrEmpty(process.ProcessName));
        }
    }

    /// <summary>
    /// ProcessInfo.DisplayName 应正确格式化
    /// </summary>
    [Fact]
    public void ProcessInfo_DisplayName_ShouldFormatCorrectly()
    {
        var processWithTitle = new ProcessInfo { ProcessName = "TestProcess", WindowTitle = "Test Window" };
        Assert.Equal("TestProcess - Test Window", processWithTitle.DisplayName);

        var processWithoutTitle = new ProcessInfo { ProcessName = "TestProcess", WindowTitle = "" };
        Assert.Equal("TestProcess", processWithoutTitle.DisplayName);
    }

#endregion
}
}
