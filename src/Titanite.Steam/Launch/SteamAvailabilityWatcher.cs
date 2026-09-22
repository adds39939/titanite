using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Launch;

internal sealed class SteamAvailabilityWatcher : IGameLauncherAvailabilityWatcher, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly ILauncherAvailabilityProbe _launchOptions;

    private readonly ILogger<SteamAvailabilityWatcher> _logger;

    private readonly CancellationTokenSource _stopping = new();

    private LauncherAvailability _current = LauncherAvailability.Unknown;

    public SteamAvailabilityWatcher(
        ILauncherAvailabilityProbe launchOptions,
        ILogger<SteamAvailabilityWatcher> logger)
    {
        _launchOptions = launchOptions;
        _logger = logger;

        _ = WatchAsync();
    }

    public LauncherAvailability Current => _current;

    public event Action<LauncherAvailability>? Changed;

    private async Task WatchAsync()
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                var latest = await ReadAsync().ConfigureAwait(false);

                if (latest != _current)
                {
                    _current = latest;

                    Changed?.Invoke(latest);
                }
            }
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<LauncherAvailability> ReadAsync()
    {
        try
        {
            return await _launchOptions.GetAvailabilityAsync(_stopping.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogDebug(e, "Could not read whether Steam can be saved to.");

            return new LauncherAvailability(
                AvailabilityStatus.Blocked,
                "Steam could not be reached, so nothing can be saved.");
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
