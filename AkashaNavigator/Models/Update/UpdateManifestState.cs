using System.Text.Json.Serialization;

namespace AkashaNavigator.Models.Update;

/// <summary>
/// 更新清单的 HTTP 条件请求状态。
/// </summary>
public sealed class UpdateManifestState
{
    [JsonPropertyName("etag")]
    public string? ETag { get; set; }

    public string? LastModified { get; set; }
}
