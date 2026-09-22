using Titanite.Core.Games;

namespace Titanite.Steam.Artwork;

internal sealed class SteamCdnArtworkService : IArtworkSource
{
    private const string CdnRoot = "https://cdn.cloudflare.steamstatic.com/steam/apps";

    public Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        var fileName = kind switch
        {
            GameArtworkKind.Capsule => "library_600x900.jpg",
            GameArtworkKind.Header => "header.jpg",
            _ => null
        };

        return Task.FromResult(fileName is null ? null : $"{CdnRoot}/{appId}/{fileName}");
    }
}
