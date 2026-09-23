using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;
using Titanite.Storage.Presets;

namespace Titanite.Storage.Tests;

public sealed class PresetServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-presets-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private readonly ILaunchOptionsStore _steam = A.Fake<ILaunchOptionsStore>();

    private readonly Dictionary<GameId, string> _stored = [];

    private readonly Dictionary<GameId, string> _builds = [];

    private readonly IGameLauncher _launcher = A.Fake<IGameLauncher>();

    private static GameId Game(uint appId) => new("steam", appId.ToString());

    public PresetServiceTests()
    {
        A.CallTo(() => _steam.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<GameId> ids, CancellationToken _) =>
                (IReadOnlyDictionary<GameId, LaunchOptions>)ids.ToDictionary(
                    id => id,
                    id => LaunchOptions.Parse(_stored.GetValueOrDefault(id, string.Empty))));

        A.CallTo(() => _launcher.GetCompatibilityToolAssignmentsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => new CompatibilityToolAssignments { ByGame = _builds });

        SteamAccepts(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));
    }

    private void SteamAccepts(LaunchOptionsSaveResult result) =>
        A.CallTo(() => _steam.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .Returns(result);

    private PresetService CreateService() =>
        new(TitaniteStorage.At(_root), _steam, _launcher, NullLogger<PresetService>.Instance);

    [Fact]
    public async Task StartsWithNothingButGlobal()
    {
        var presets = await CreateService().GetAllAsync();

        Assert.Single(presets);
        Assert.True(presets[0].IsGlobal);
        Assert.Equal("Global", presets[0].Name);
        Assert.True(presets[0].Options.IsEmpty);
    }

    [Fact]
    public async Task WillNotRemoveGlobal()
    {
        var service = CreateService();

        await service.DeleteAsync(PresetId.Global);

        Assert.Single(await service.GetAllAsync());
        Assert.False(Preset.Global.CanBeRemoved);
    }

    [Fact]
    public async Task CreatesAPresetWithItsOwnIdentity()
    {
        var created = await CreateService().CreateAsync("Handheld");

        Assert.Equal("Handheld", created.Name);
        Assert.False(created.IsGlobal);
        Assert.NotEmpty(created.Id);
    }

    [Fact]
    public async Task KeepsCreatedPresetsAcrossRestarts()
    {
        await CreateService().CreateAsync("Handheld");

        Assert.Equal(["Global", "Handheld"], (await CreateService().GetAllAsync()).Select(preset => preset.Name));
    }

    [Fact]
    public async Task PutsGlobalFirstAndSortsTheRest()
    {
        var service = CreateService();

        await service.CreateAsync("Zen");
        await service.CreateAsync("Anticheat");

        Assert.Equal(
            ["Global", "Anticheat", "Zen"],
            (await service.GetAllAsync()).Select(preset => preset.Name));
    }

    [Fact]
    public async Task WillNotHandOutTheSameNameTwice()
    {
        var service = CreateService();

        await service.CreateAsync("Handheld");

        Assert.Equal("Handheld 2", (await service.CreateAsync("Handheld")).Name);
    }

    [Fact]
    public async Task RemovesAPresetAndLetsItsGamesGo()
    {
        var service = CreateService();
        var preset = await service.CreateAsync("Handheld");

        await service.ApplyAsync(Game(620), preset.Id);
        await service.DeleteAsync(preset.Id);

        Assert.Equal(["Global"], (await service.GetAllAsync()).Select(candidate => candidate.Name));
        Assert.Null(await service.AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task RemembersWhichPresetAGameUses()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);

        Assert.Equal(PresetId.Global, await CreateService().AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task ForgetsAPresetWhenAGameIsGivenNone()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);
        await service.ApplyAsync(Game(620), null);

        Assert.Null(await service.AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task IgnoresAPresetThatDoesNotExist()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), "made-up");

        Assert.Null(await service.AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task SavesAPresetNothingUsesWithoutTouchingTheLauncher()
    {
        var service = CreateService();

        var result = await service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        Assert.True(result.IsSuccess);
        Assert.Equal("MANGOHUD=1 %command%", (await CreateService().GetAsync(PresetId.Global)).Options.Format());

        A.CallTo(() => _steam.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PushesAChangedPresetOutToEveryGameUsingIt()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);
        await service.ApplyAsync(Game(400), PresetId.Global);

        await service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        A.CallTo(() => _steam.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>.That.Matches(batch =>
                    batch.Count == 2 &&
                    batch[Game(620)].Format() == "MANGOHUD=1 %command%" &&
                    batch[Game(400)].Format() == "MANGOHUD=1 %command%"),
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PushesThePresetsProtonBuildOutTheSameWay()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);

        await service.SaveAndApplyAsync(Preset.Global with { CompatibilityTool = "GE-Proton11-3" });

        A.CallTo(() => _steam.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>.That.Matches(tools =>
                    tools.Contains(new KeyValuePair<GameId, string>(Game(620), "GE-Proton11-3"))),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task KeepsTheOldPresetWhenTheLauncherRefusesTheChange()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);

        SteamAccepts(new LaunchOptionsSaveResult(
            LaunchOptionsSaveStatus.LauncherUnavailable,
            "Steam is not running."));

        var result = await service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        Assert.False(result.IsSuccess);
        Assert.True((await CreateService().GetAsync(PresetId.Global)).Options.IsEmpty);
    }

    [Fact]
    public async Task ListsTheGamesUsingAPreset()
    {
        var service = CreateService();
        var handheld = await service.CreateAsync("Handheld");

        await service.ApplyAsync(Game(620), PresetId.Global);
        await service.ApplyAsync(Game(400), handheld.Id);

        Assert.Equal([Game(400)], await service.GamesUsingAsync(handheld.Id));
        Assert.Equal([Game(620)], await service.GamesUsingAsync(PresetId.Global));
    }

    [Fact]
    public async Task EmptiesAPresetAndReleasesItsGamesOnReset()
    {
        var service = CreateService();

        await service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        await service.ApplyAsync(Game(620), PresetId.Global);
        await service.ResetAsync(PresetId.Global);

        Assert.True((await service.GetAsync(PresetId.Global)).Options.IsEmpty);
        Assert.Null(await service.AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task RenamesAPresetButNeverGlobal()
    {
        var service = CreateService();
        var preset = await service.CreateAsync("Handheld");

        Assert.Equal("Docked", (await service.RenameAsync(preset.Id, "Docked")).Name);
        Assert.Equal("Global", (await service.RenameAsync(PresetId.Global, "Everything")).Name);
    }

    [Fact]
    public async Task LetsGoOfAGameWhoseOptionsNoLongerMatchItsPreset()
    {
        var service = CreateService();

        await service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        await service.ApplyAsync(Game(620), PresetId.Global);
        await service.ApplyAsync(Game(400), PresetId.Global);

        _stored[Game(620)] = "MANGOHUD=1 %command%";
        _stored[Game(400)] = "DXVK_HUD=1 %command%";

        Assert.Equal(1, await service.ReconcileAsync());
        Assert.Equal(PresetId.Global, await service.AppliedToAsync(Game(620)));
        Assert.Null(await service.AppliedToAsync(Game(400)));
    }

    [Fact]
    public async Task LetsGoOfAGameWhoseProtonBuildNoLongerMatchesItsPreset()
    {
        var service = CreateService();

        await service.SaveAndApplyAsync(Preset.Global with { CompatibilityTool = "GE-Proton11-3" });
        await service.ApplyAsync(Game(620), PresetId.Global);

        _builds[Game(620)] = "GE-Proton11-3";

        Assert.Equal(0, await service.ReconcileAsync());

        _builds[Game(620)] = "Proton-CachyOS";

        Assert.Equal(1, await service.ReconcileAsync());
        Assert.Null(await service.AppliedToAsync(Game(620)));
    }

    [Fact]
    public async Task LetsGoOfAGameGivenABuildWhereThePresetAskedForNone()
    {
        var service = CreateService();

        await service.ApplyAsync(Game(620), PresetId.Global);

        Assert.Equal(0, await service.ReconcileAsync());

        _builds[Game(620)] = "GE-Proton11-3";

        Assert.Equal(1, await service.ReconcileAsync());
    }

    [Fact]
    public async Task KeepsAPresetAppliedWhileTheCheckWasAskingSteam()
    {
        var service = CreateService();
        var applying = false;

        await service.ApplyAsync(Game(620), PresetId.Global);

        _stored[Game(620)] = "MANGOHUD=1 %command%";

        A.CallTo(() => _steam.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<GameId> ids, CancellationToken _) =>
            {
                if (!applying)
                {
                    applying = true;
                    service.ApplyAsync(Game(400), PresetId.Global).GetAwaiter().GetResult();
                }

                return (IReadOnlyDictionary<GameId, LaunchOptions>)ids.ToDictionary(
                    id => id,
                    id => LaunchOptions.Parse(_stored.GetValueOrDefault(id, string.Empty)));
            });

        Assert.Equal(1, await service.ReconcileAsync());
        Assert.Null(await service.AppliedToAsync(Game(620)));
        Assert.Equal(PresetId.Global, await service.AppliedToAsync(Game(400)));
    }

    [Fact]
    public async Task KeepsAPresetAppliedWhileAnotherWasBeingSaved()
    {
        var service = CreateService();
        var steamSaving = new TaskCompletionSource<LaunchOptionsSaveResult>();

        await service.ApplyAsync(Game(620), PresetId.Global);

        A.CallTo(() => _steam.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .Returns(steamSaving.Task);

        var saving = service.SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        var applying = service.ApplyAsync(Game(400), PresetId.Global);

        steamSaving.SetResult(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));

        await Task.WhenAll(saving, applying);

        Assert.Equal(PresetId.Global, await service.AppliedToAsync(Game(400)));
        Assert.Equal("MANGOHUD=1 %command%", (await service.GetAsync(PresetId.Global)).Options.Format());
    }

    [Fact]
    public async Task HasNothingToReconcileWhenNoGameUsesAPreset() =>
        Assert.Equal(0, await CreateService().ReconcileAsync());
}
