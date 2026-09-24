using Photino.NET;

namespace Titanite.App.Hosting;

internal static class WindowMinimumSize
{
    private const int Width = 800;
    private const int Height = 560;

    public static void Apply(PhotinoWindow window, int interfaceScale)
    {
        var workArea = window.MainMonitor.WorkArea.Size;

        window.SetMinSize(
            Math.Min(Scale(Width, interfaceScale), workArea.Width),
            Math.Min(Scale(Height, interfaceScale), workArea.Height));
    }

    private static int Scale(int size, int interfaceScale) => size * interfaceScale / 100;
}
