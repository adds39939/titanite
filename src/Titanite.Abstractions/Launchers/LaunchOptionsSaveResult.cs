namespace Titanite.Abstractions.Launchers;

public enum LaunchOptionsSaveStatus
{
    Saved,
    LauncherUnavailable,
    WriteFailed
}

public sealed record LaunchOptionsSaveResult(LaunchOptionsSaveStatus Status, string? Message = null)
{
    public bool IsSuccess => Status == LaunchOptionsSaveStatus.Saved;
}
