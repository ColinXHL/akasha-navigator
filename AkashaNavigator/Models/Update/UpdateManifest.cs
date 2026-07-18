using System.Text.Json.Serialization;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 应用更新清单。
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>
    /// 稳定版应用更新信息。
    /// </summary>
    public AppUpdateChannelInfo? Stable { get; set; }

    /// <summary>
    /// 测试版应用更新信息。
    /// </summary>
    public AppUpdateChannelInfo? Alpha { get; set; }

    /// <summary>
    /// 最低允许使用的宿主版本。
    /// </summary>
    [JsonPropertyName("min_required_version")]
    public string? MinRequiredVersion { get; set; }

}

/// <summary>
/// 单个应用更新频道的信息。
/// </summary>
public sealed class AppUpdateChannelInfo
{
    public string? Version { get; set; }

    public string? Installer { get; set; }

    /// <summary>
    /// 安装包存储源，不是更新程序的频道 ID。
    /// </summary>
    public string? Source { get; set; }

    public string? Notes { get; set; }
}
