namespace Titanite.UI.Services.Formatting;

public static class FileSizeDisplay
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown";
        }

        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {Units[unit]}";
    }
}
