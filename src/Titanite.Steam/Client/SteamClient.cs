using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Processes;

namespace Titanite.Steam.Client;

internal sealed class SteamClient(ILogger<SteamClient> logger, IHostProcesses hostProcesses) : ISteamClient
{
    private const string ProcessName = "steam";

    private const string GameLaunchMarker = "SteamLaunch AppId=";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public bool IsRunning() => hostProcesses.IsRunning(ProcessName);

    public bool IsGameRunning() => hostProcesses.AnyCommandLineContains(GameLaunchMarker);

    public async Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsRunning())
        {
            return true;
        }

        logger.LogInformation("Asking Steam to shut down.");

        if (!TryRun("-shutdown"))
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!IsRunning())
            {
                logger.LogInformation("Steam has exited.");

                return true;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning("Steam was still running {Timeout} after being asked to shut down.", timeout);

        return false;
    }

    public bool Start()
    {
        logger.LogInformation("Starting Steam.");

        return TryRun();
    }

    public bool LaunchGame(uint appId)
    {
        logger.LogInformation("Asking Steam to launch app {AppId}.", appId);

        return TryRun(GameUrl(appId));
    }

    internal static string GameUrl(uint appId) => $"steam://rungameid/{appId}";

    private bool TryRun(params string[] arguments) =>
        hostProcesses.Start(ProcessName, arguments);
}
