using Titanite.Core.Games;
using Titanite.Core.Launch;

namespace Titanite.Abstractions.Launchers;

public interface ILaunchOptionsStore
{
    Task<LaunchOptions> GetAsync(GameId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<GameId, LaunchOptions>> GetManyAsync(
        IReadOnlyCollection<GameId> ids,
        CancellationToken cancellationToken = default);

    Task<LaunchOptionsSaveResult> SaveAsync(
        GameId id,
        LaunchOptions options,
        CancellationToken cancellationToken = default);

    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<GameId, LaunchOptions> optionsByGame,
        CancellationToken cancellationToken = default);

    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<GameId, LaunchOptions> optionsByGame,
        IReadOnlyDictionary<GameId, string> compatibilityToolsByGame,
        CancellationToken cancellationToken = default);
}
