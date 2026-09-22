using Titanite.Core.Games;
using Titanite.Core.Presets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Titanite.Storage.Presets;

internal sealed record StoredPresets
{
    public IReadOnlyList<StoredPreset> Presets { get; init; } = [];

    public IReadOnlyDictionary<string, string> Applied { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static StoredPresets Empty { get; } = new()
    {
        Presets = [StoredPreset.From(Preset.Global)]
    };

    public static StoredPresets FromProfile(StoredProfile profile) => new()
    {
        Presets =
        [
            StoredPreset.From(Preset.Global with { Options = profile.LaunchOptions.ToLaunchOptions() })
        ],
        Applied = profile.LinkedGames.ToDictionary(
            game => game.ToString(),
            _ => PresetId.Global,
            StringComparer.Ordinal)
    };

    public StoredPresets Sanitised()
    {
        List<StoredPreset> presets = [.. Presets.Where(preset => preset.Id is { Length: > 0 })];

        if (!presets.Any(preset => PresetId.IsGlobal(preset.Id)))
        {
            presets.Insert(0, StoredPreset.From(Preset.Global));
        }

        var known = presets.Select(preset => preset.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return this with
        {
            Presets = presets,
            Applied = Applied
                .Where(pair => known.Contains(pair.Value) && GameId.TryParse(pair.Key, out _))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };
    }

    public IReadOnlyList<Preset> InOrder() =>
    [
        .. Presets
            .Select(preset => preset.ToPreset())
            .OrderByDescending(preset => preset.IsGlobal)
            .ThenBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
    ];

    public IReadOnlyDictionary<GameId, string> Assignments()
    {
        var assignments = new Dictionary<GameId, string>();

        foreach (var (game, preset) in Applied)
        {
            if (GameId.TryParse(game, out var id))
            {
                assignments[id] = preset;
            }
        }

        return assignments;
    }
}

internal sealed record StoredPreset
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public StoredLaunchOptions LaunchOptions { get; init; } = new();

    public string CompatibilityTool { get; init; } = string.Empty;

    public static StoredPreset From(Preset preset) => new()
    {
        Id = preset.Id,
        Name = preset.Name,
        LaunchOptions = StoredLaunchOptions.From(preset.Options),
        CompatibilityTool = preset.CompatibilityTool
    };

    public Preset ToPreset() => new()
    {
        Id = Id,
        Name = PresetId.IsGlobal(Id) ? PresetId.GlobalName : Name,
        Options = LaunchOptions.ToLaunchOptions(),
        CompatibilityTool = CompatibilityTool
    };
}

internal static class StoredPresetsContext
{
    public static JsonSerializerOptions Presets { get; } = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new StoredLaunchOptionsJsonConverter());

        return options;
    }
}
