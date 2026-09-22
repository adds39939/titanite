namespace Titanite.Platform.Processes;

public static class FlatpakSandbox
{
    private const string InfoFile = "/.flatpak-info";

    public static bool IsActive => File.Exists(InfoFile);
}
