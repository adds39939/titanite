using Microsoft.Extensions.Logging;
using Titanite.Steam.Vdf;
using System.Diagnostics;

namespace Titanite.Steam.Client;

internal sealed class SteamClient(ILogger<SteamClient> logger) : ISteamClient
{
    private const string ProcessName = "steam";

    private const string DetachCommand = "setsid";

    private const string GameLaunchMarker = "SteamLaunch AppId=";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    public bool IsRunning()
    {
        try
        {
            return Process.GetProcessesByName(ProcessName).Length > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(e, "Could not determine whether Steam is running.");

            return false;
        }
    }

    public bool IsGameRunning()
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                var name = Path.GetFileName(directory);

                if (!int.TryParse(name, out _))
                {
                    continue;
                }

                string commandLine;

                try
                {
                    commandLine = File.ReadAllText(Path.Combine(directory, "cmdline"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (commandLine.Replace('\0', ' ').Contains(GameLaunchMarker, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not scan for running games.");
        }

        return false;
    }

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
        TryStart(BuildStartInfo(detached: true, arguments)) ||
        TryStart(BuildStartInfo(detached: false, arguments));

    private bool TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);

            return process is not null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not run {FileName}.", startInfo.FileName);

            return false;
        }
    }

    internal static ProcessStartInfo BuildStartInfo(bool detached, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(detached ? DetachCommand : ProcessName)
        {
            UseShellExecute = false
        };

        if (detached)
        {
            startInfo.ArgumentList.Add("--fork");
            startInfo.ArgumentList.Add(ProcessName);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
