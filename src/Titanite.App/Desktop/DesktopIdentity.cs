using System.Runtime.InteropServices;

namespace Titanite.App.Desktop;

internal static class DesktopIdentity
{
    public const string ApplicationId = "io.github.adds39939.Titanite";

    [DllImport("libglib-2.0.so.0")]
    private static extern void g_set_prgname(string prgname);

    public static void Apply() => g_set_prgname(ApplicationId);
}
