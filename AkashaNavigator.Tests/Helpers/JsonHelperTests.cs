using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Helpers;
using Xunit;

namespace AkashaNavigator.Tests.Helpers;

public class JsonHelperTests
{
    [Fact]
    public void SaveToFile_ReturnsFailure_WhenPathIsEmpty()
    {
        var result = JsonHelper.SaveToFile(string.Empty, new { Name = "Test" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void LoadFromFile_ReturnsFailure_WhenFileDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var result = JsonHelper.LoadFromFile<Dictionary<string, string>>(missingPath);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SaveToFile_ReturnsFailure_WhenPathContainsInvalidChars()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "invalid<name>.json");

        var result = JsonHelper.SaveToFile(invalidPath, new { Name = "Test" });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_PATH", result.Error!.Code);
    }
}
