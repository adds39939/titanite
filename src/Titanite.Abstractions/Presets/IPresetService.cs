using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Presets;

namespace Titanite.Abstractions.Presets;

public interface IPresetService
{
    Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Preset> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<Preset> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<Preset> RenameAsync(string id, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
        Preset preset,
        CancellationToken cancellationToken = default);

    Task ResetAsync(string id, CancellationToken cancellationToken = default);

    Task<string?> AppliedToAsync(GameId id, CancellationToken cancellationToken = default);

    Task ApplyAsync(GameId id, string? presetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<GameId, string>> GetAssignmentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameId>> GamesUsingAsync(string id, CancellationToken cancellationToken = default);

    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);
}
