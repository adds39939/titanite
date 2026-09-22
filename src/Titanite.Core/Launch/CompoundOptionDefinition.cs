namespace Titanite.Core.Launch;

public sealed record CompoundOptionDefinition(string Key, string Label)
{
    public SettingKind Kind { get; init; } = SettingKind.Toggle;

    public IReadOnlyList<string> Choices { get; init; } = [];

    public string? Placeholder { get; init; }

    public string? Description { get; init; }
}
