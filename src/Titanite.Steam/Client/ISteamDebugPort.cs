namespace Titanite.Steam.Client;

internal interface ISteamDebugPort
{
    Task<bool> IsListeningAsync(CancellationToken cancellationToken = default);

    Task<bool> WaitUntilListeningAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
