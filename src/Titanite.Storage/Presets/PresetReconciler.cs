using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;

namespace Titanite.Storage.Presets;

public sealed class PresetReconciler : IPresetReconciler, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IPresetService _presets;

    private readonly IGameConfigurationWatcher _watcher;

    private readonly ILogger<PresetReconciler> _logger;

    private readonly CancellationTokenSource _stopping = new();

    private readonly SemaphoreSlim _running = new(1, 1);

    private HashSet<GameId> _followed = [];

    public PresetReconciler(
        IPresetService presets,
        IGameConfigurationWatcher watcher,
        ILogger<PresetReconciler> logger)
    {
        _presets = presets;
        _watcher = watcher;
        _logger = logger;

        _watcher.Changed += OnChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await ReconcileAsync(cancellationToken).ConfigureAwait(false);

        _ = FollowAsync();
    }

    private void OnChanged(GameId id) => _ = ReconcileAsync(_stopping.Token);

    private async Task FollowAsync()
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
            {
                await FollowAssignedGamesAsync(_stopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var dropped = await _presets.ReconcileAsync(cancellationToken).ConfigureAwait(false);

            if (dropped > 0)
            {
                _logger.LogInformation(
                    "{GameCount} games were changed outside Titanite, so they no longer use a preset.",
                    dropped);
            }

            await FollowAssignedGamesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "The presets could not be checked against what the launcher has stored.");
        }
        finally
        {
            _running.Release();
        }
    }

    private async Task FollowAssignedGamesAsync(CancellationToken cancellationToken)
    {
        var assigned = (await _presets.GetAssignmentsAsync(cancellationToken).ConfigureAwait(false))
            .Keys
            .ToHashSet();

        foreach (var id in assigned.Except(_followed))
        {
            _watcher.Follow(id);
        }

        foreach (var id in _followed.Except(assigned))
        {
            _watcher.Drop(id);
        }

        _followed = assigned;
    }

    public void Dispose()
    {
        _watcher.Changed -= OnChanged;

        _stopping.Cancel();
        _stopping.Dispose();
        _running.Dispose();
    }
}
