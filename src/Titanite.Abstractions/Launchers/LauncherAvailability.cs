namespace Titanite.Abstractions.Launchers;

public enum AvailabilityStatus
{
    Unknown,
    Available,
    Blocked
}

public sealed record LauncherAvailability(AvailabilityStatus Status, string? Explanation)
{
    public static LauncherAvailability Unknown { get; } = new(AvailabilityStatus.Unknown, null);

    public bool IsAvailable => Status == AvailabilityStatus.Available;
}
