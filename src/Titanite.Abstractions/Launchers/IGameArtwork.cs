using Titanite.Core.Games;

namespace Titanite.Abstractions.Launchers;

public interface IGameArtwork
{
    Task<string?> GetArtworkSourceAsync(
        GameId id,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default);
}
