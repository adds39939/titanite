namespace Titanite.Abstractions.Launchers;

public sealed record LauncherCapabilities
{
    public static LauncherCapabilities None { get; } = new();

    public bool CanLaunchGames { get; init; }

    public bool CanSaveLaunchOptions { get; init; }

    public bool CanAssignCompatibilityTools { get; init; }

    public bool ReportsInstallLocation { get; init; }
}
