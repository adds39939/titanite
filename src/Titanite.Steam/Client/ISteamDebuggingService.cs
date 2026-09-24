namespace Titanite.Steam.Client;

internal interface ISteamDebuggingService
{
    Task<SteamDebuggingOutcome> EnsureEnabledAsync(CancellationToken cancellationToken = default);
}

public enum SteamDebuggingOutcome
{
    AlreadyEnabled,
    EnabledAndRestarted,
    EnabledPendingStart,
    EnabledPendingRestart,
    NoSteamInstall,
    Failed
}
