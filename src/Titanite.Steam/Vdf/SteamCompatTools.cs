namespace Titanite.Steam.Vdf;

internal static class SteamCompatTools
{
    public static readonly string[] MappingRoot =
        ["InstallConfigStore", "Software", "Valve", "Steam", "CompatToolMapping"];

    public const string NameKey = "name";

    public static string ConfigPathIn(string steamRoot) => Path.Combine(steamRoot, "config", "config.vdf");
}
