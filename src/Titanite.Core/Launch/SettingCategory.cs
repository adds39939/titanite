namespace Titanite.Core.Launch;

public sealed record SettingCategory(string Id, string Title, int Order)
{
    public CommandDefinition? Command { get; init; }

    public bool Is(string id) => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase);
}

public static class SettingCategoryIds
{
    public const string Cpu = "cpu";

    public const string MangoHud = "mangohud";
}
