namespace Titanite.Steam.Library;

internal sealed class SteamInstallLocator : ISteamInstallLocator
{
    public string? Locate()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(xdgDataHome))
        {
            xdgDataHome = Path.Combine(home, ".local", "share");
        }

        string[] candidates =
        [
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".steam", "root"),
            Path.Combine(xdgDataHome, "Steam"),
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".steam", "debian-installation"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam")
        ];

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var resolved = Resolve(candidate);

            if (resolved is null || !seen.Add(resolved))
            {
                continue;
            }

            if (Directory.Exists(Path.Combine(resolved, "steamapps")))
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? Resolve(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);

            if (!directory.Exists)
            {
                return null;
            }

            return directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
