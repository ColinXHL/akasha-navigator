namespace AkashaNavigator.Models.Common;

public class AppUpdateCheckResult
{
    public bool HasUpdate { get; set; }

    public string CurrentVersion { get; set; } = string.Empty;

    public string TargetVersion { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool IsPrerelease { get; set; }

    public string SourceId { get; set; } = "cnb";

    public static AppUpdateCheckResult NoUpdate(string currentVersion)
    {
        return new AppUpdateCheckResult { HasUpdate = false, CurrentVersion = currentVersion };
    }

    public static AppUpdateCheckResult WithUpdate(string currentVersion, string targetVersion, string notes,
                                                   bool isPrerelease, string sourceId)
    {
        return new AppUpdateCheckResult {
            HasUpdate = true,
            CurrentVersion = currentVersion,
            TargetVersion = targetVersion,
            Notes = notes,
            IsPrerelease = isPrerelease,
            SourceId = sourceId
        };
    }
}
