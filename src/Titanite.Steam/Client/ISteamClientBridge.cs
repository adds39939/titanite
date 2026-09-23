namespace Titanite.Steam.Client;

internal interface ISteamClientBridge
{
    Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default);
}

internal interface ISteamClientSession : IAsyncDisposable
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    Task<SteamAppDetails?> GetAppDetailsAsync(uint appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<uint, SteamAppDetails>> GetAppDetailsAsync(
        IReadOnlyCollection<uint> appIds,
        CancellationToken cancellationToken = default);

    Task<bool> SetLaunchOptionsAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default);

    Task<bool> SetCompatToolAsync(uint appId, string toolName, CancellationToken cancellationToken = default);
}
