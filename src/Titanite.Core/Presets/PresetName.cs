namespace Titanite.Core.Presets;

public static class PresetName
{
    public const int MaximumLength = 48;

    public static string Clean(string? name) =>
        (name ?? string.Empty).Trim() is { Length: > 0 } trimmed
            ? trimmed.Length > MaximumLength ? trimmed[..MaximumLength].TrimEnd() : trimmed
            : string.Empty;

    public static bool IsTaken(string name, IEnumerable<Preset> presets) =>
        presets.Any(preset => string.Equals(preset.Name, name, StringComparison.CurrentCultureIgnoreCase));

    public static string Unique(string name, IEnumerable<Preset> presets)
    {
        var taken = presets.ToArray();

        if (!IsTaken(name, taken))
        {
            return name;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{name} {suffix}";

            if (!IsTaken(candidate, taken))
            {
                return candidate;
            }
        }
    }
}
