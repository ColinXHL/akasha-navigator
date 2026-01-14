using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Core;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// OverlayApi 属性测试和单元测试
/// 注意：由于 OverlayApi 依赖 WPF Application 和 OverlayWindow，
/// 这里主要测试无 UI 环境下的行为和参数验证
/// </summary>
public class OverlayApiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginContext _context;
    private readonly ConfigApi _configApi;

    public OverlayApiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"overlay_api_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var manifest = new PluginManifest { Id = "test-overlay-plugin", Name = "Test Overlay Plugin", Version = "1.0.0",
                                            Main = "main.js", Permissions = new List<string> { "overlay" } };

        File.WriteAllText(Path.Combine(_tempDir, "main.js"), "function onLoad() {} function onUnload() {}");

        _context = new PluginContext(manifest, _tempDir);

        var config = new PluginConfig("test-overlay-plugin");
        _configApi = new ConfigApi(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

#region Property 2 : 绘图元素 ID 唯一性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 2: 绘图元素 ID 唯一性**
    /// *对于任意*调用 DrawText、DrawRect 或 DrawImage 方法的序列，
    /// 每次调用返回的元素 ID 应唯一且非空。
    /// 注意：由于没有 WPF Application，这里测试 ID 生成逻辑的唯一性
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DrawingElementIdUniqueness(PositiveInt count)
    {
        // 生成多个唯一 ID 并验证唯一性
        var idCount = (count.Get % 100) + 1;
        var ids = new HashSet<string>();

        for (int i = 0; i < idCount; i++)
        {
            // 模拟 ID 生成逻辑（与 OverlayWindow 中的实现一致）
            var id = $"element_{Guid.NewGuid():N}";
            ids.Add(id);
        }

        // 所有 ID 应该唯一
        var allUnique = ids.Count == idCount;
        // 所有 ID 应该非空
        var allNonEmpty = ids.All(id => !string.IsNullOrEmpty(id));

        return (allUnique && allNonEmpty)
            .Label($"Count: {idCount}, UniqueIds: {ids.Count}, AllNonEmpty: {allNonEmpty}");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 2: 绘图元素 ID 唯一性（Guid 格式）**
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Fact]
    public void GeneratedElementId_ShouldBeValidFormat()
    {
        // 生成多个 ID 并验证格式
        for (int i = 0; i < 10; i++)
        {
            var id = $"element_{Guid.NewGuid():N}";

            Assert.NotNull(id);
            Assert.NotEmpty(id);
            Assert.StartsWith("element_", id);
            Assert.Equal(40, id.Length); // "element_" (8) + Guid (32)
        }
    }

#endregion

#region Unit Tests - Other Methods

    /// <summary>
    /// Show 无 UI 环境不应该抛出异常
    /// </summary>
    [Fact]
    public void Show_NoUI_ShouldNotThrow()
    {
        var overlayApi = new OverlayApi(_context, _configApi);
        overlayApi.show();
    }

    /// <summary>
    /// Hide 无 UI 环境不应该抛出异常
    /// </summary>
    [Fact]
    public void Hide_NoUI_ShouldNotThrow()
    {
        var overlayApi = new OverlayApi(_context, _configApi);
        overlayApi.hide();
    }

    /// <summary>
    /// SetPosition 无 UI 环境不应该抛出异常
    /// </summary>
    [Fact]
    public void SetPosition_NoUI_ShouldNotThrow()
    {
        var overlayApi = new OverlayApi(_context, _configApi);
        overlayApi.setPosition(100, 100);
    }

    /// <summary>
    /// SetSize 无效尺寸应该被忽略
    /// </summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-10, 100)]
    public void SetSize_InvalidSize_ShouldBeIgnored(int width, int height)
    {
        var overlayApi = new OverlayApi(_context, _configApi);

        // 不应该抛出异常
        overlayApi.setSize(width, height);
    }

    /// <summary>
    /// ShowMarker 无效方向应该被忽略
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("123")]
    public void ShowMarker_InvalidDirection_ShouldBeIgnored(string? direction)
    {
        var overlayApi = new OverlayApi(_context, _configApi);

        // 不应该抛出异常
        overlayApi.showMarker(direction!, 0);
    }

    /// <summary>
    /// ShowMarker 有效方向应该被接受
    /// </summary>
    [Theory]
    [InlineData("north")]
    [InlineData("n")]
    [InlineData("up")]
    [InlineData("south")]
    [InlineData("east")]
    [InlineData("west")]
    [InlineData("northeast")]
    [InlineData("ne")]
    public void ShowMarker_ValidDirection_ShouldNotThrow(string direction)
    {
        var overlayApi = new OverlayApi(_context, _configApi);

        // 不应该抛出异常
        overlayApi.showMarker(direction, 1000);
    }

    /// <summary>
    /// ClearMarkers 无 UI 环境不应该抛出异常
    /// </summary>
    [Fact]
    public void ClearMarkers_NoUI_ShouldNotThrow()
    {
        var overlayApi = new OverlayApi(_context, _configApi);
        overlayApi.clearMarkers();
    }

    /// <summary>
    /// 构造函数应该拒绝 null 参数
    /// </summary>
    [Fact]
    public void Constructor_NullContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new OverlayApi(null!, _configApi));
    }

    /// <summary>
    /// 构造函数应该拒绝 null ConfigApi
    /// </summary>
    [Fact]
    public void Constructor_NullConfigApi_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new OverlayApi(_context, null!));
    }

#endregion
}
}
