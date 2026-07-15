namespace AkashaNavigator.Models.Plugin;

public sealed record CompanionStatus(
    bool Running,
    string State,
    int? ProcessId = null,
    string? Error = null);
