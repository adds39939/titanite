using System.Runtime.InteropServices;

namespace Titanite.App;

internal static class WebKitEnvironment
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setenv(string name, string value, int overwrite);

    public static void DisableNvidiaExplicitSync() => setenv("__NV_DISABLE_EXPLICIT_SYNC", "1", 0);

    public static void EnsureOsIsLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This application is only supported on Linux");
        }
    }
}
