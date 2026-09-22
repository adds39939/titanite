using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Hosting;

namespace Titanite.Bootstrap.Startup;

internal sealed class AppStartupService(
    IEnumerable<IStartupStep> steps,
    ILogger<AppStartupService> logger) : IAppStartupService
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var step in steps)
        {
            try
            {
                await step.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "{Step} did not finish. The application is starting anyway.", step.Name);
            }
        }
    }
}
