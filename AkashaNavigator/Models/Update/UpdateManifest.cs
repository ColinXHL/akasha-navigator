using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 应用与插件共用的远程更新清单。
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>
    /// 清单结构版本。旧版 Notice 缺少该字段时保持为 1。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

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

    /// <summary>
    /// 远程插件目录，键为插件 ID。
    /// </summary>
    public Dictionary<string, RemotePluginInfo> Plugins { get; set; } = new();
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
