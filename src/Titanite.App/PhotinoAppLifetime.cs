using Microsoft.Extensions.Logging;
using Photino.NET;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Processes;

namespace Titanite.App;

internal sealed class PhotinoAppLifetime(IHostProcesses host, ILogger<PhotinoAppLifetime> logger) : IAppLifetime
{
    private PhotinoWindow? _window;

    public void Attach(PhotinoWindow window) => _window = window;

    public bool Restart()
    {
        if (_window is not { } window)
        {
            return false;
        }

        if (!host.Start("flatpak", ["run", DesktopIdentity.ApplicationId]))
        {
            logger.LogWarning("Could not start Titanite again, so it is staying open.");

            return false;
        }

        logger.LogInformation("Restarting Titanite.");
        window.Invoke(window.Close);

        return true;
    }
}
