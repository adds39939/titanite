using Titanite.Core.Games;

namespace Titanite.Abstractions.Launchers;

public interface IGameLibrary
{
    Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken cancellationToken = default);

    void Invalidate();
}
