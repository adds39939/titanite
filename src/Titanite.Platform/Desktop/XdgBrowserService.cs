using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Desktop;
using Titanite.Platform.Processes;

namespace Titanite.Platform.Desktop;

public sealed class XdgBrowserService(ILogger<XdgBrowserService> logger) : IBrowserService
{
    private const string OpenCommand = "xdg-open";

    public bool Open(Uri address)
    {
        if (!address.IsAbsoluteUri || address.Scheme is not ("https" or "http"))
        {
            logger.LogWarning("Refusing to open {Address} in the browser.", address);

            return false;
        }

        logger.LogInformation("Opening {Address} in the browser.", address);

        return DetachedProcess.TryStart(logger, OpenCommand, [address.AbsoluteUri]);
    }
}
