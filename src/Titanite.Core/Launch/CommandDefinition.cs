namespace Titanite.Core.Launch;

public sealed record CommandFlagGroup(string? Name, IReadOnlyList<CommandFlagDefinition> Flags);

public sealed record CommandFlagDefinition(string Flag, string Label)
{
    public SettingKind Kind { get; init; } = SettingKind.Toggle;

    public IReadOnlyList<string> Choices { get; init; } = [];

    public string? Placeholder { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool TakesValue => Kind != SettingKind.Toggle;

    public IEnumerable<string> Spellings => Aliases.Prepend(Flag);

    public bool Matches(string token) => Spellings.Contains(token, StringComparer.Ordinal);
}

public sealed record CommandDefinition(string Command, string Label)
{
    public string? Description { get; init; }

    public string? Terminator { get; init; }

    public IReadOnlyList<CommandFlagGroup> Groups { get; init; } = [];

    public IEnumerable<CommandFlagDefinition> AllFlags => Groups.SelectMany(group => group.Flags);
}
