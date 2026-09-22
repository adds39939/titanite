namespace Titanite.Core.Launch;

public readonly record struct SettingSearch
{
    private SettingSearch(string term) => Term = term;

    public static SettingSearch None => default;

    public static SettingSearch For(string? term) => new(term?.Trim() ?? string.Empty);

    public string Term { get; }

    public bool IsActive => Term is { Length: > 0 };

    public bool Matches(SettingDefinition definition) =>
        !IsActive ||
        Contains(definition.Variable) ||
        Contains(definition.Label) ||
        Contains(definition.Description);

    public bool Matches(CommandFlagDefinition flag) =>
        !IsActive ||
        flag.Spellings.Any(Contains) ||
        Contains(flag.Label) ||
        Contains(flag.Description);

    public bool Matches(CommandDefinition command) =>
        !IsActive ||
        Contains(command.Command) ||
        Contains(command.Label) ||
        Contains(command.Description);

    public bool MatchesVariable(string name) => !IsActive || Contains(name);

    public bool MatchesAnythingIn(CommandDefinition command) =>
        Matches(command) || command.AllFlags.Any(Matches);

    private bool Contains(string? text) =>
        text is not null && text.Contains(Term, StringComparison.OrdinalIgnoreCase);
}
