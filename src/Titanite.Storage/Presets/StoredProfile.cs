using System.Text.Json;
using System.Text.Json.Serialization;
using Titanite.Core.Games;
using Titanite.Core.Launch;

namespace Titanite.Storage.Presets;

internal sealed record StoredProfile
{
    public StoredLaunchOptions LaunchOptions { get; init; } = new();

    public IReadOnlyList<GameId> LinkedGames { get; init; } = [];

    [JsonPropertyName("LinkedApps")]
    public IReadOnlyList<GameId>? LinkedApps { get; init; }

    public StoredProfile Upgraded() =>
        LinkedApps is { Count: > 0 } legacy && LinkedGames.Count == 0
            ? this with { LinkedGames = legacy, LinkedApps = null }
            : this with { LinkedApps = null };
}

internal sealed record StoredLaunchOptions
{
    public IReadOnlyList<StoredVariable> Environment { get; init; } = [];

    public IReadOnlyList<string> Wrapper { get; init; } = [];

    public bool HasCommandPlaceholder { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public static StoredLaunchOptions From(LaunchOptions options) => new()
    {
        Environment =
        [
            .. options.Environment.Select(variable =>
                new StoredVariable(variable.Name, variable.Value) { OriginalText = variable.OriginalText })
        ],
        Wrapper = [.. options.Wrapper],
        HasCommandPlaceholder = options.HasCommandPlaceholder,
        Arguments = [.. options.Arguments]
    };

    public LaunchOptions ToLaunchOptions() => LaunchOptions.Parse(Format());

    private string Format() => new LaunchOptions
    {
        Environment =
        [
            .. Environment.Select(variable =>
                new EnvironmentVariable(variable.Name, variable.Value) { OriginalText = variable.OriginalText })
        ],
        Wrapper = [.. Wrapper],
        HasCommandPlaceholder = HasCommandPlaceholder,
        Arguments = [.. Arguments]
    }.Format();
}

internal sealed record StoredVariable(string Name, string Value)
{
    public string? OriginalText { get; init; }
}

internal sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetUInt32(out var appId))
        {
            return new GameId(LegacyLauncher, appId.ToString());
        }

        return GameId.TryParse(reader.GetString(), out var id) ? id : default;
    }

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    internal const string LegacyLauncher = "steam";
}

internal sealed class StoredLaunchOptionsJsonConverter : JsonConverter<StoredLaunchOptions>
{
    public override StoredLaunchOptions Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return StoredLaunchOptions.From(LaunchOptions.Parse(reader.GetString()));
        }

        return JsonSerializer.Deserialize<StoredLaunchOptions>(ref reader, StoredProfileContext.Shape)
               ?? new StoredLaunchOptions();
    }

    public override void Write(Utf8JsonWriter writer, StoredLaunchOptions value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, StoredProfileContext.Shape);
}

internal static class StoredProfileContext
{
    public static JsonSerializerOptions Shape { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions Profile { get; } = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new GameIdJsonConverter());
        options.Converters.Add(new StoredLaunchOptionsJsonConverter());

        return options;
    }
}
