using Titanite.Core.Launch;

namespace Titanite.Core.Presets;

public sealed record Preset
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public LaunchOptions Options { get; init; } = new();

    public string CompatibilityTool { get; init; } = string.Empty;

    public bool IsGlobal => PresetId.IsGlobal(Id);

    public bool CanBeRemoved => !IsGlobal;

    public static Preset Global { get; } = new() { Id = PresetId.Global, Name = PresetId.GlobalName };

    public bool Matches(LaunchOptions options, string compatibilityTool) =>
        string.Equals(Options.Format(), options.Format(), StringComparison.Ordinal) &&
        string.Equals(CompatibilityTool, compatibilityTool, StringComparison.OrdinalIgnoreCase);
}
