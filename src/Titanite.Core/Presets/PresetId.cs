namespace Titanite.Core.Presets;

public static class PresetId
{
    public const string Global = "global";

    public const string GlobalName = "Global";

    public static bool IsGlobal(string? id) =>
        string.Equals(id, Global, StringComparison.OrdinalIgnoreCase);

    public static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
