using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Proton;
using Titanite.Steam.Launch;

namespace Titanite.Steam.Tests;

public sealed class SteamConfigurationWatcherTests
{
    private static readonly GameId Portal2 = new("steam", "620");

    private readonly ILaunchOptionsStore _launchOptions = A.Fake<ILaunchOptionsStore>();

    private readonly IGameLauncher _launcher = A.Fake<IGameLauncher>();

    private readonly Dictionary<GameId, string> _stored = [];

    private readonly Dictionary<GameId, string> _builds = [];

    public SteamConfigurationWatcherTests()
    {
        A.CallTo(() => _launchOptions.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<GameId> ids, CancellationToken _) =>
                (IReadOnlyDictionary<GameId, LaunchOptions>)ids.ToDictionary(
                    id => id,
                    id => LaunchOptions.Parse(_stored.GetValueOrDefault(id, string.Empty))));

        A.CallTo(() => _launcher.GetCompatibilityToolAssignmentsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => new CompatibilityToolAssignments { ByGame = _builds });
    }

    private SteamConfigurationWatcher CreateWatcher() =>
        new(_launchOptions, _launcher, NullLogger<SteamConfigurationWatcher>.Instance);

    [Fact]
    public async Task SaysNothingAboutTheFirstThingItSees()
    {
        _stored[Portal2] = "MANGOHUD=1 %command%";

        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();

        Assert.Empty(changed);
    }

    [Fact]
    public async Task ReportsLaunchOptionsThatMovedUnderneathIt()
    {
        _stored[Portal2] = "MANGOHUD=1 %command%";

        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();

        _stored[Portal2] = "DXVK_HUD=1 %command%";

        await watcher.SweepAsync();

        Assert.Equal([Portal2], changed);
    }

    [Fact]
    public async Task ReportsAProtonBuildThatMovedUnderneathIt()
    {
        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();

        _builds[Portal2] = "GE-Proton11-3";

        await watcher.SweepAsync();

        Assert.Equal([Portal2], changed);
    }

    [Fact]
    public async Task StaysQuietWhileNothingMoves()
    {
        _stored[Portal2] = "MANGOHUD=1 %command%";

        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();
        await watcher.SweepAsync();
        await watcher.SweepAsync();

        Assert.Empty(changed);
    }

    [Fact]
    public async Task ReportsEachChangeRatherThanOnlyTheFirst()
    {
        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();

        _stored[Portal2] = "MANGOHUD=1 %command%";
        await watcher.SweepAsync();

        _stored[Portal2] = "DXVK_HUD=1 %command%";
        await watcher.SweepAsync();

        Assert.Equal([Portal2, Portal2], changed);
    }

    [Fact]
    public async Task SaysNothingAboutAGameItWasToldToDrop()
    {
        using var watcher = CreateWatcher();
        var changed = new List<GameId>();

        watcher.Changed += changed.Add;
        watcher.Follow(Portal2);

        await watcher.SweepAsync();

        watcher.Drop(Portal2);
        _stored[Portal2] = "DXVK_HUD=1 %command%";

        await watcher.SweepAsync();

        Assert.Empty(changed);
    }

    [Fact]
    public async Task ReadsNothingWhileItFollowsNothing()
    {
        using var watcher = CreateWatcher();

        await watcher.SweepAsync();

        A.CallTo(() => _launchOptions.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CarriesOnWhenTheLauncherCannotBeRead()
    {
        A.CallTo(() => _launchOptions.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .Throws(new IOException("localconfig.vdf is locked"));

        using var watcher = CreateWatcher();

        watcher.Follow(Portal2);

        await watcher.SweepAsync();
    }
}
