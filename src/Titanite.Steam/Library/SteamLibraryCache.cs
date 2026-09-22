using Titanite.Core.Games;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Library;

internal static class SteamLibraryCache
{
    public static string RootIn(string steamRoot) => Path.Combine(steamRoot, "appcache", "librarycache");

    public static IReadOnlyList<string> FileNamesFor(GameArtworkKind kind) => kind switch
    {
        GameArtworkKind.Capsule => ["library_600x900.jpg", "library_capsule.jpg"],
        GameArtworkKind.Header => ["header.jpg", "library_header.jpg"],
        _ => []
    };

    public static string? Find(string steamRoot, uint appId, GameArtworkKind kind)
    {
        var names = FileNamesFor(kind);

        if (names.Count == 0)
        {
            return null;
        }

        var appDirectory = Path.Combine(RootIn(steamRoot), appId.ToString());

        try
        {
            if (!Directory.Exists(appDirectory))
            {
                return null;
            }

            foreach (var directory in Directories(appDirectory))
            {
                foreach (var name in names)
                {
                    var candidate = Path.Combine(directory, name);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Directories(string appDirectory) =>
        new[] { appDirectory }.Concat(Directory.EnumerateDirectories(appDirectory));
}
