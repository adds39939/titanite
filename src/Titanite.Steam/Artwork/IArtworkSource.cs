using Titanite.Core.Games;

namespace Titanite.Steam.Artwork;

internal interface IArtworkSource
{
    Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default);
}
