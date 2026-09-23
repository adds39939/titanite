using Titanite.Abstractions.Hosting;

namespace Titanite.DebugServer;

internal sealed class WebAppLifetime(ILogger<WebAppLifetime> logger) : IAppLifetime
{
    public bool Restart()
    {
        logger.LogInformation("The debug server does not restart itself.");

        return false;
    }
}
