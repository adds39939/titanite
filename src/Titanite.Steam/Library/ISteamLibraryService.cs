namespace Titanite.Steam.Library;

internal interface ISteamLibraryService
{
    Task<IReadOnlyList<SteamApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);

    void Invalidate();
}
