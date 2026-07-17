using System;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 更新清单服务配置。
/// </summary>
public sealed class UpdateOptions
{
    /// <summary>
    /// 远程更新清单地址。
    /// </summary>
    public string ManifestUrl { get; set; } = "https://update.fisheepx.cn/notice.json";

    /// <summary>
    /// 后续周期刷新使用的默认间隔。
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// 单次更新清单网络请求超时。
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
