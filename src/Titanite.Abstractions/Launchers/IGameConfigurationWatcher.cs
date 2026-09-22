using Titanite.Core.Games;

namespace Titanite.Abstractions.Launchers;

public interface IGameConfigurationWatcher
{
    event Action<GameId> Changed;

    void Follow(GameId id);

    void Drop(GameId id);
}
