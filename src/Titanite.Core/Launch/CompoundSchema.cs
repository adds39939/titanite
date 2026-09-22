namespace Titanite.Core.Launch;

public sealed record CompoundOptionGroup(string? Name, IReadOnlyList<CompoundOptionDefinition> Options);

public sealed record CompoundSchema(
    string Separator,
    string Assignment,
    IReadOnlyList<CompoundOptionGroup> Groups)
{
    public const string DefaultSeparator = ",";

    public const string DefaultAssignment = "=";

    private Dictionary<string, CompoundOptionDefinition>? _byKey;

    public IEnumerable<CompoundOptionDefinition> AllOptions => Groups.SelectMany(group => group.Options);

    public CompoundOptionDefinition? Find(string key)
    {
        _byKey ??= AllOptions
            .GroupBy(option => option.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return _byKey.GetValueOrDefault(key);
    }
}
