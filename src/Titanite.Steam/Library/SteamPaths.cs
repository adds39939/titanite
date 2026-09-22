using System.Globalization;

namespace Titanite.Steam.Library;

internal static class SteamPaths
{
    private const string SteamApps = "steamapps";

    public static string CompatDataDirectory(string libraryPath, uint appId) =>
        Path.Combine(
            libraryPath,
            SteamApps,
            "compatdata",
            appId.ToString(CultureInfo.InvariantCulture));

    public static string PrefixDirectory(string libraryPath, uint appId) =>
        Path.Combine(CompatDataDirectory(libraryPath, appId), "pfx");
}
