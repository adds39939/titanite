namespace Titanite.Core.Launch;

public sealed record CompoundEntry(string Key, string? Value)
{
    public string Render(CompoundSchema schema) =>
        Value is null ? Key : $"{Key}{schema.Assignment}{Value}";
}

public sealed record CompoundValue
{
    public required CompoundSchema Schema { get; init; }

    public IReadOnlyList<CompoundEntry> Entries { get; init; } = [];

    public bool IsEmpty => Entries.Count == 0;

    public static CompoundValue Parse(CompoundSchema schema, string? value)
    {
        var entries = new List<CompoundEntry>();

        foreach (var entry in (value ?? string.Empty).Split(schema.Separator, StringSplitOptions.TrimEntries))
        {
            if (entry.Length == 0)
            {
                continue;
            }

            var separator = entry.IndexOf(schema.Assignment, StringComparison.Ordinal);

            if (separator < 0)
            {
                if (entries.Count > 0 && entry.All(char.IsAsciiDigit) && entries[^1].Value is { } previous)
                {
                    entries[^1] = entries[^1] with { Value = $"{previous}{schema.Separator}{entry}" };

                    continue;
                }

                entries.Add(new CompoundEntry(entry, null));

                continue;
            }

            entries.Add(new CompoundEntry(entry[..separator], entry[(separator + schema.Assignment.Length)..]));
        }

        return new CompoundValue { Schema = schema, Entries = entries };
    }

    public string Format() =>
        string.Join(Schema.Separator, Entries.Select(entry => entry.Render(Schema)));

    public bool Contains(string key) =>
        Entries.Any(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

    public string? GetValue(string key) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal))?.Value;

    public CompoundValue Set(string key, string? value)
    {
        var entries = Entries.ToList();
        var index = entries.FindIndex(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

        if (index >= 0)
        {
            entries[index] = new CompoundEntry(key, value);
        }
        else
        {
            entries.Add(new CompoundEntry(key, value));
        }

        return this with { Entries = entries };
    }

    public CompoundValue Remove(string key) =>
        this with
        {
            Entries = Entries
                .Where(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal))
                .ToList()
        };

    public IReadOnlyList<CompoundOptionGroup> GroupsWithValues() => Schema.Groups
        .Select(group => group with
        {
            Options = group.Options.Where(option => Contains(option.Key)).ToList()
        })
        .Where(group => group.Options.Count > 0)
        .ToList();

    public IReadOnlyList<CompoundEntry> Unrecognised =>
        Entries.Where(entry => Schema.Find(entry.Key) is null).ToList();

    public CompoundValue ReplaceUnrecognised(IEnumerable<CompoundEntry> replacements)
    {
        var recognised = Entries.Where(entry => Schema.Find(entry.Key) is not null);

        return this with { Entries = [.. recognised, .. replacements] };
    }
}
