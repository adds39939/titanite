using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Hosting;
using Titanite.Steam.Client;

namespace Titanite.Bootstrap.Startup;

internal sealed class LauncherDebuggingStep(
    ISteamDebuggingService debugging,
    ILogger<LauncherDebuggingStep> logger) : IStartupStep
{
    public string Name => "The launcher debugging check";

    public string Activity => "Connecting to Steam…";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await debugging.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Steam debugging check: {Outcome}.", outcome);
    }
}
