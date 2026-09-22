namespace Titanite.UI.Services.Formatting;

public static class PathDisplay
{
    private static readonly string HomeDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Abbreviate(string path) =>
        !string.IsNullOrEmpty(HomeDirectory) && path.StartsWith(HomeDirectory, StringComparison.Ordinal)
            ? string.Concat("~", path.AsSpan(HomeDirectory.Length))
            : path;
}
