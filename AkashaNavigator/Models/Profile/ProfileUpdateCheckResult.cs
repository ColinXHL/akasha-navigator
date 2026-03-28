namespace AkashaNavigator.Models.Profile
{
/// <summary>
/// Profile 更新检查结果。
/// </summary>
public class ProfileUpdateCheckResult
{
    public string ProfileId { get; set; } = string.Empty;

    public string ProfileName { get; set; } = string.Empty;

    public string CurrentVersion { get; set; } = string.Empty;

    public string? AvailableVersion { get; set; }

    public bool HasUpdate { get; set; }

    public MarketplaceProfile Profile { get; set; } = new();
}
}
