namespace Titanite.Steam.Client;

internal interface ISteamClient
{
    bool IsRunning();

    bool IsGameRunning();

    Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    bool Start();

    bool LaunchGame(uint appId);
}
