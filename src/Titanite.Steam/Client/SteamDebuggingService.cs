using Microsoft.Extensions.Logging;
using Titanite.Steam.Library;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Client;

internal sealed class SteamDebuggingService(
    ISteamInstallLocator installLocator,
    ISteamClient steamClient,
    ISteamDebugPort debugPort,
    ILogger<SteamDebuggingService> logger) : ISteamDebuggingService
{
    public const string MarkerFileName = ".cef-enable-remote-debugging";

    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    public async Task<SteamDebuggingOutcome> EnsureEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (installLocator.Locate() is not { } steamRoot)
        {
            logger.LogWarning("No Steam installation was found, so its debugging interface cannot be enabled.");

            return SteamDebuggingOutcome.NoSteamInstall;
        }

        var markerPath = Path.Combine(steamRoot, MarkerFileName);

        if (File.Exists(markerPath))
        {
            return SteamDebuggingOutcome.AlreadyEnabled;
        }

        try
        {
            await File.WriteAllTextAsync(markerPath, string.Empty, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Asked Steam for its debugging interface at {MarkerPath}.", markerPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write {MarkerPath}.", markerPath);

            return SteamDebuggingOutcome.Failed;
        }

        return await RestartSteamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SteamDebuggingOutcome> RestartSteamAsync(CancellationToken cancellationToken)
    {
        if (!steamClient.IsRunning())
        {
            return SteamDebuggingOutcome.EnabledPendingStart;
        }

        if (steamClient.IsGameRunning())
        {
            logger.LogInformation("A game is running, so Steam was left alone.");

            return SteamDebuggingOutcome.EnabledPendingRestart;
        }

        if (!await steamClient.ShutdownAsync(ShutdownTimeout, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Steam did not close, so it is still running without its debugging interface.");

            return SteamDebuggingOutcome.EnabledPendingRestart;
        }

        steamClient.Start();

        await debugPort.WaitUntilListeningAsync(ActivationTimeout, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Steam was closed and started again with its debugging interface.");

        return SteamDebuggingOutcome.EnabledAndRestarted;
    }
}
