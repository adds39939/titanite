using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;

namespace Titanite.Steam.Launch;

internal sealed class SteamConfigurationWatcher : IGameConfigurationWatcher, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly ILaunchOptionsStore _launchOptions;

    private readonly IGameLauncher _launcher;

    private readonly ILogger<SteamConfigurationWatcher> _logger;

    private readonly CancellationTokenSource _stopping = new();

    private readonly Lock _gate = new();

    private readonly HashSet<GameId> _followed = [];

    private readonly Dictionary<GameId, string> _seen = [];

    public SteamConfigurationWatcher(
        ILaunchOptionsStore launchOptions,
        IGameLauncher launcher,
        ILogger<SteamConfigurationWatcher> logger)
    {
        _launchOptions = launchOptions;
        _launcher = launcher;
        _logger = logger;

        _ = WatchAsync();
    }

    public event Action<GameId>? Changed;

    public void Follow(GameId id)
    {
        if (id.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            _followed.Add(id);
        }
    }

    public void Drop(GameId id)
    {
        lock (_gate)
        {
            _followed.Remove(id);
            _seen.Remove(id);
        }
    }

    private async Task WatchAsync()
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
            {
                await SweepAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal async Task SweepAsync()
    {
        GameId[] following;

        lock (_gate)
        {
            following = [.. _followed];
        }

        if (following.Length == 0)
        {
            return;
        }

        try
        {
            var options = await _launchOptions.GetManyAsync(following, _stopping.Token).ConfigureAwait(false);

            var assignments = await _launcher
                .GetCompatibilityToolAssignmentsAsync(_stopping.Token)
                .ConfigureAwait(false);

            foreach (var id in following)
            {
                var stored = $"{assignments.For(id) ?? string.Empty}\n{(options.GetValueOrDefault(id) ?? new()).Format()}";

                bool changed;

                lock (_gate)
                {
                    changed = _followed.Contains(id) &&
                              _seen.TryGetValue(id, out var last) &&
                              !string.Equals(last, stored, StringComparison.Ordinal);

                    if (_followed.Contains(id))
                    {
                        _seen[id] = stored;
                    }
                }

                if (changed)
                {
                    _logger.LogInformation("{GameId} was changed outside Titanite.", id);

                    Changed?.Invoke(id);
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogDebug(e, "Could not read what the launcher has stored for the games being followed.");
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
