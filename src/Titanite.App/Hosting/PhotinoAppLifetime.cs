using Microsoft.Extensions.Logging;
using PhotinoX.App;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Processes;
using Titanite.App.Desktop;

namespace Titanite.App.Hosting;

internal sealed class PhotinoAppLifetime(IHostProcesses host, ILogger<PhotinoAppLifetime> logger) : IAppLifetime
{
    public bool Restart()
    {
        if (!host.Start("flatpak", ["run", DesktopIdentity.ApplicationId]))
        {
            logger.LogWarning("Could not start Titanite again, so it is staying open.");

            return false;
        }

        logger.LogInformation("Restarting Titanite.");

        var window = PhotinoApp.Current.MainWindow;

        window.Invoke(window.Close);

        return true;
    }
}
