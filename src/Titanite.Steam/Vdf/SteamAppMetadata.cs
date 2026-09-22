namespace Titanite.Steam.Vdf;

internal sealed record SteamAppMetadata(string? Type, string? OperatingSystems)
{
    private const string ToolType = "Tool";

    private const string Linux = "linux";

    public bool IsTool => string.Equals(Type, ToolType, StringComparison.OrdinalIgnoreCase);

    public bool RunsOnLinux => OperatingSystems is { } list &&
                               list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                   .Any(name => string.Equals(name, Linux, StringComparison.OrdinalIgnoreCase));
}
