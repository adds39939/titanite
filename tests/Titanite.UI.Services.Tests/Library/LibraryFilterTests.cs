using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Abstractions.Settings;
using Titanite.Core.Games;
using Titanite.Core.Presets;
using Titanite.Core.Settings;
using Titanite.UI.Services.Library;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Services.Tests.Library;

public sealed class LibraryFilterTests
{
    private readonly IGameLibrary _library = A.Fake<IGameLibrary>();

    private readonly IAppSettingsService _settings = A.Fake<IAppSettingsService>();

    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private AppSettings _stored = new();

    public LibraryFilterTests()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).ReturnsLazily(() => _stored);
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Invokes((AppSettings saved, CancellationToken _) => _stored = saved);

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global]);
    }

    [Fact]
    public void NamesEachFilterForTheThingItLetsThrough()
    {
        Assert.Equal("Show native games", LibraryFilter.NativeGames.Title());
        Assert.Equal("Show tools", LibraryFilter.Tools.Title());
    }

    [Fact]
    public void KeepsAWindowsGameWhateverTheFiltersSay()
    {
        Assert.True(LibraryFilters.Admits(Windows, showNativeGames: false, showTools: false));
        Assert.True(LibraryFilters.Admits(Windows, showNativeGames: true, showTools: true));
    }

    [Fact]
    public void HidesANativeGameUntilItIsAskedFor()
    {
        Assert.False(LibraryFilters.Admits(Native, showNativeGames: false, showTools: false));
        Assert.True(LibraryFilters.Admits(Native, showNativeGames: true, showTools: false));
    }

    [Fact]
    public void HidesAToolUntilItIsAskedFor()
    {
        Assert.False(LibraryFilters.Admits(Tool, showNativeGames: false, showTools: false));
        Assert.True(LibraryFilters.Admits(Tool, showNativeGames: false, showTools: true));
    }

    [Fact]
    public void LetsTheToolFilterSpeakForAToolThatRunsNatively()
    {
        var tool = Tool with { RunsNatively = true };

        Assert.True(LibraryFilters.Admits(tool, showNativeGames: false, showTools: true));
        Assert.False(LibraryFilters.Admits(tool, showNativeGames: true, showTools: false));
    }

    [Fact]
    public async Task OpensWithNativeGamesAndToolsHidden()
    {
        Installed(Windows, Native, Tool);

        var presenter = await Load();

        Assert.False(presenter.ShowNativeGames);
        Assert.False(presenter.ShowTools);
        Assert.Equal(["Cyberpunk 2077"], presenter.VisibleGames.Select(game => game.Name));
    }

    [Fact]
    public async Task CountsOnlyWhatTheFiltersLetThrough()
    {
        Installed(Windows, Native, Tool);

        var presenter = await Load();

        Assert.Equal("1 game installed", presenter.Subtitle);

        await presenter.ShowAsync(LibraryFilter.Tools, true);

        Assert.Equal("2 games installed", presenter.Subtitle);
    }

    [Fact]
    public async Task ShowsNativeGamesOnceAskedTo()
    {
        Installed(Windows, Native, Tool);

        var presenter = await Load();

        await presenter.ShowAsync(LibraryFilter.NativeGames, true);

        Assert.True(presenter.IsShowing(LibraryFilter.NativeGames));
        Assert.Equal(["Cyberpunk 2077", "Dota 2"], presenter.VisibleGames.Select(game => game.Name));
    }

    [Fact]
    public async Task RemembersEachFilterForNextTime()
    {
        Installed(Windows, Native, Tool);

        await (await Load()).ShowAsync(LibraryFilter.Tools, true);

        Assert.True(_stored.ShowTools);
        Assert.False(_stored.ShowNativeGames);

        var reopened = await Load();

        Assert.True(reopened.IsShowing(LibraryFilter.Tools));
        Assert.False(reopened.IsShowing(LibraryFilter.NativeGames));
    }

    [Fact]
    public async Task SaysTheFiltersAreWhatEmptiedTheList()
    {
        Installed(Native, Tool);

        var presenter = await Load();

        Assert.Empty(presenter.VisibleGames);
        Assert.Equal("Every installed game is hidden by the filters.", presenter.EmptyMessage);
    }

    [Fact]
    public async Task StillAsksAboutSteamWhenNothingIsInstalledAtAll()
    {
        Installed();

        var presenter = await Load();

        Assert.Contains("Is Steam installed", presenter.EmptyMessage);
    }

    [Fact]
    public async Task SearchesOnlyWhatTheFiltersLetThrough()
    {
        Installed(Windows, Native, Tool);

        var presenter = await Load();

        presenter.SearchTerm = "Dota";

        Assert.Empty(presenter.VisibleGames);

        await presenter.ShowAsync(LibraryFilter.NativeGames, true);

        Assert.Equal(["Dota 2"], presenter.VisibleGames.Select(game => game.Name));
    }

    private async Task<GameLibraryPresenter> Load()
    {
        var presenter = new GameLibraryPresenter(_library, _settings, _presets);

        await presenter.LoadAsync();

        return presenter;
    }

    private void Installed(params GameEntry[] games) =>
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>(games);

    private static GameEntry Windows => Game(1091500, "Cyberpunk 2077");

    private static GameEntry Native => Game(570, "Dota 2") with { RunsNatively = true };

    private static GameEntry Tool => Game(1826330, "Proton EasyAntiCheat Runtime") with { IsTool = true };

    private static GameEntry Game(uint appId, string name) => new()
    {
        Id = new GameId("steam", appId.ToString()),
        Name = name,
        InstallDirectory = $"/games/common/{name}",
        IsFullyInstalled = true
    };
}
