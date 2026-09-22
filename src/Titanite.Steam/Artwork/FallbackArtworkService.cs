using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;

namespace Titanite.Steam.Artwork;

internal sealed class FallbackArtworkService : IGameArtwork
{
    private readonly IReadOnlyList<IArtworkSource> _sources;

    public FallbackArtworkService(IEnumerable<IArtworkSource> sources) => _sources = [.. sources];

    public async Task<string?> GetArtworkSourceAsync(
        GameId id,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        if (!SteamIds.TryAppId(id, out var appId))
        {
            return null;
        }

        foreach (var source in _sources)
        {
            if (await source.GetArtworkSourceAsync(appId, kind, cancellationToken) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
