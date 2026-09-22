using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Storage.Presets;

namespace Titanite.Storage.Tests;

public sealed class PresetReconcilerTests
{
    private static readonly GameId Portal2 = new("steam", "620");

    private static readonly GameId Portal = new("steam", "400");

    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private readonly FakeConfigurationWatcher _watcher = new();

    private readonly Dictionary<GameId, string> _assigned = [];

    private int _checks;

    public PresetReconcilerTests()
    {
        A.CallTo(() => _presets.GetAssignmentsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => (IReadOnlyDictionary<GameId, string>)_assigned);

        A.CallTo(() => _presets.ReconcileAsync(A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                Interlocked.Increment(ref _checks);

                return 0;
            });
    }

    private PresetReconciler CreateReconciler() =>
        new(_presets, _watcher, NullLogger<PresetReconciler>.Instance);

    [Fact]
    public async Task ChecksEveryPresetAgainstTheLauncherOnStartUp()
    {
        using var reconciler = CreateReconciler();

        await reconciler.StartAsync();

        Assert.Equal(1, _checks);
    }

    [Fact]
    public async Task FollowsEveryGameThatUsesAPreset()
    {
        _assigned[Portal2] = "global";
        _assigned[Portal] = "handheld";

        using var reconciler = CreateReconciler();

        await reconciler.StartAsync();

        Assert.Equal([Portal, Portal2], _watcher.Following.OrderBy(id => id.Id));
    }

    [Fact]
    public async Task ChecksAgainWhenAGameIsChangedOutsideTheApp()
    {
        _assigned[Portal2] = "global";

        using var reconciler = CreateReconciler();

        await reconciler.StartAsync();

        _watcher.Report(Portal2);

        Assert.True(await WaitUntil(() => Volatile.Read(ref _checks) == 2));
    }

    [Fact]
    public async Task StopsFollowingAGameThatNoLongerUsesAPreset()
    {
        _assigned[Portal2] = "global";

        using var reconciler = CreateReconciler();

        await reconciler.StartAsync();

        Assert.Equal([Portal2], _watcher.Following);

        _assigned.Remove(Portal2);
        _watcher.Report(Portal2);

        Assert.True(await WaitUntil(() => _watcher.Following.Count == 0));
    }

    [Fact]
    public async Task CarriesOnWhenTheCheckCannotBeMade()
    {
        A.CallTo(() => _presets.ReconcileAsync(A<CancellationToken>._))
            .Throws(new IOException("presets.json is locked"));

        _assigned[Portal2] = "global";

        using var reconciler = CreateReconciler();

        await reconciler.StartAsync();
    }

    [Fact]
    public async Task SaysNothingToTheWatcherOnceItIsDoneWith()
    {
        _assigned[Portal2] = "global";

        var reconciler = CreateReconciler();

        await reconciler.StartAsync();

        reconciler.Dispose();

        _watcher.Report(Portal2);

        Assert.False(await WaitUntil(() => Volatile.Read(ref _checks) > 1));
    }

    private static async Task<bool> WaitUntil(Func<bool> settled)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (settled())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return settled();
    }

    private sealed class FakeConfigurationWatcher : IGameConfigurationWatcher
    {
        private readonly HashSet<GameId> _following = [];

        public IReadOnlyCollection<GameId> Following => [.. _following];

        public event Action<GameId>? Changed;

        public void Follow(GameId id) => _following.Add(id);

        public void Drop(GameId id) => _following.Remove(id);

        public void Report(GameId id) => Changed?.Invoke(id);
    }
}
