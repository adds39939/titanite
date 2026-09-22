namespace Titanite.Core.Launch;

public sealed record SettingGroup(string? Name, IReadOnlyList<SettingDefinition> Settings);
