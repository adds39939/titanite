using Titanite.Core.Games;

namespace Titanite.Core.Proton;

public sealed record CompatibilityToolAssignments
{
    public static CompatibilityToolAssignments None { get; } = new()
    {
        ByGame = new Dictionary<GameId, string>()
    };

    public required IReadOnlyDictionary<GameId, string> ByGame { get; init; }

    public string? Default { get; init; }

    public IEnumerable<string> AllToolNames =>
        Default is null ? ByGame.Values : ByGame.Values.Append(Default);

    public string? For(GameId id) => ByGame.GetValueOrDefault(id);

    public IReadOnlyList<GameId> GamesUsing(string toolName) =>
        [.. ByGame
            .Where(pair => string.Equals(pair.Value, toolName, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)];
}
