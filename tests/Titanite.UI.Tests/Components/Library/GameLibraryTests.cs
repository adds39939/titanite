using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Abstractions.Settings;
using Titanite.Core.Games;
using Titanite.Core.Presets;
using Titanite.Core.Settings;
using Titanite.UI.Components.Library;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Tests.Components.Library;

public sealed class GameLibraryTests : BunitContext
{
    private readonly IGameLibrary _library = A.Fake<IGameLibrary>();

    private readonly IAppSettingsService _settings = A.Fake<IAppSettingsService>();

    private readonly IPresetService _presets = A.Fake<IPresetService>();

    public GameLibraryTests()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new AppSettings());
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>([]);

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global]);

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(_library);
        Services.AddSingleton(_settings);
        Services.AddSingleton(A.Fake<IGameArtwork>());
        Services.AddTransient<IGameLibraryPresenter>(_ =>
            new GameLibraryPresenter(_library, _settings, _presets));
    }

    [Fact]
    public void SaysItIsScanningWhileItReads()
    {
        var reading = new TaskCompletionSource<IReadOnlyList<GameEntry>>();

        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._)).Returns(reading.Task);

        var library = Render<GameLibrary>();

        Assert.Equal("Scanning game libraries…", library.Find(".library-message").TextContent);
        Assert.True(library.Find(".library-header button").HasAttribute("disabled"));

        reading.SetResult([]);
    }

    [Fact]
    public void ExplainsAFailedRead()
    {
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Throws(new IOException("config.vdf is locked"));

        var library = Render<GameLibrary>();

        Assert.Contains("config.vdf is locked", library.Find(".library-message-error").TextContent);
    }

    [Fact]
    public void AsksWhetherSteamIsInstalledWhenItFindsNothing()
    {
        var library = Render<GameLibrary>();

        Assert.Contains("Is Steam installed", library.Find(".library-message").TextContent);
        Assert.Empty(library.FindAll(".game-grid, .game-list"));
    }

    [Fact]
    public void ListsWhatItFound()
    {
        Installed(Game(620, "Portal 2"), Game(400, "Portal"));

        var library = Render<GameLibrary>();

        Assert.Empty(library.FindAll(".library-message"));
        Assert.Equal(2, library.FindAll(".game-list > .game-row").Count);
        Assert.Contains("2 games installed", library.Find(".library-subtitle").TextContent);
    }

    [Fact]
    public void SaysWhenASearchMatchesNothing()
    {
        Installed(Game(620, "Portal 2"));

        var library = Render<GameLibrary>();

        library.Find(".search").Input("half-life");

        library.WaitForAssertion(() =>
            Assert.Equal("No games match that search.", library.Find(".library-message").TextContent));
    }

    [Fact]
    public void MatchesOnTheIdAsWellAsTheName()
    {
        Installed(Game(620, "Portal 2"));

        var library = Render<GameLibrary>();

        library.Find(".search").Input("620");

        library.WaitForAssertion(() => Assert.Single(library.FindAll(".game-list > .game-row")));
    }

    [Fact]
    public void WaitsForTypingToPauseBeforeSearching()
    {
        Installed(Game(620, "Portal 2"), Game(400, "Portal"));

        var library = Render<GameLibrary>();

        library.Find(".search").Input("6");
        library.Find(".search").Input("62");

        Assert.Equal(2, library.FindAll(".game-list > .game-row").Count);

        library.WaitForAssertion(() => Assert.Single(library.FindAll(".game-list > .game-row")));
    }

    [Fact]
    public void UsesTheLibraryAlreadyReadWhenOpened()
    {
        Render<GameLibrary>();

        A.CallTo(() => _library.Invalidate()).MustNotHaveHappened();
    }

    [Fact]
    public void GoesBackToDiskWhenAskedToRescan()
    {
        var library = Render<GameLibrary>();

        library.Find(".library-header button").Click();

        A.CallTo(() => _library.Invalidate()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void RemembersTheViewItWasLeftIn()
    {
        Installed(Game(620, "Portal 2"));

        var library = Render<GameLibrary>();

        library.FindAll(".view-button")[(int)LibraryViewMode.Grid].Click();

        Assert.Empty(library.FindAll(".game-list"));
        Assert.NotEmpty(library.FindAll(".game-grid"));

        A.CallTo(() => _settings.SaveAsync(
                A<AppSettings>.That.Matches(settings => settings.LibraryView == LibraryViewMode.Grid),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void RemembersTheOrderItWasLeftIn()
    {
        Installed(Game(620, "Portal 2"), Game(400, "Portal"));

        var library = Render<GameLibrary>();

        library.Find(".library-filters .dropdown > button").Click();
        library.FindAll(".library-filters .item")[(int)LibrarySortOrder.RecentlyPlayed].Click();

        Assert.Equal("Recently played", library.Find(".library-filters .dropdown > button").TextContent.Trim());

        A.CallTo(() => _settings.SaveAsync(
                A<AppSettings>.That.Matches(settings => settings.LibrarySort == LibrarySortOrder.RecentlyPlayed),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void ShowsWhichOrderIsInUse()
    {
        var library = Render<GameLibrary>();

        library.Find(".library-filters .dropdown > button").Click();

        var items = library.FindAll(".library-filters .item");

        Assert.Equal("true", items[(int)LibrarySortOrder.Name].GetAttribute("aria-checked"));
        Assert.Equal("false", items[(int)LibrarySortOrder.RecentlyPlayed].GetAttribute("aria-checked"));
    }

    [Fact]
    public void HidesNativeGamesAndToolsUntilTheFiltersAskForThem()
    {
        Installed(Game(1091500, "Cyberpunk 2077"), Native(570, "Dota 2"), Tool(1826330, "Proton Runtime"));

        var library = Render<GameLibrary>();

        Assert.Single(library.FindAll(".game-list > .game-row"));
        Assert.Contains("1 game installed", library.Find(".library-subtitle").TextContent);
    }

    [Fact]
    public void LetsToolsThroughOnceTheFilterIsTicked()
    {
        Installed(Game(1091500, "Cyberpunk 2077"), Tool(1826330, "Proton Runtime"));

        var library = Render<GameLibrary>();

        OpenFilters(library);
        Filter(library, LibraryFilter.Tools).Change(true);

        Assert.Equal(2, library.FindAll(".game-list > .game-row").Count);

        A.CallTo(() => _settings.SaveAsync(
                A<AppSettings>.That.Matches(settings => settings.ShowTools && !settings.ShowNativeGames),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void LeavesTheFilterMenuOpenSoASecondOneCanBeTicked()
    {
        Installed(Game(1091500, "Cyberpunk 2077"), Native(570, "Dota 2"), Tool(1826330, "Proton Runtime"));

        var library = Render<GameLibrary>();

        OpenFilters(library);
        Filter(library, LibraryFilter.Tools).Change(true);
        Filter(library, LibraryFilter.NativeGames).Change(true);

        Assert.Equal(3, library.FindAll(".game-list > .game-row").Count);
    }

    [Fact]
    public void OffersOneFilterPerThingThatCanBeHidden()
    {
        var library = Render<GameLibrary>();

        OpenFilters(library);

        Assert.Equal(
            ["Show native games", "Show tools"],
            library.FindAll(".library-filters .option-label").Select(option => option.TextContent));
    }

    [Fact]
    public void SaysWhenTheFiltersAreWhatEmptiedTheList()
    {
        Installed(Tool(1826330, "Proton Runtime"));

        var library = Render<GameLibrary>();

        Assert.Equal("Every installed game is hidden by the filters.", library.Find(".library-message").TextContent);
    }

    [Fact]
    public void BadgesEachGameWithThePresetItUses()
    {
        Installed(Game(1091500, "Cyberpunk 2077"), Game(2357570, "Overwatch"));

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global, new Preset { Id = "a", Name = "Anticheat" }]);

        A.CallTo(() => _presets.GetAssignmentsAsync(A<CancellationToken>._))
            .Returns<IReadOnlyDictionary<GameId, string>>(new Dictionary<GameId, string>
            {
                [new GameId("steam", "2357570")] = "a"
            });

        var library = Render<GameLibrary>();

        Assert.Equal(["Anticheat"], library.FindAll(".tag-preset").Select(tag => tag.TextContent));
    }

    [Fact]
    public void BadgesTheCardViewOverTheArtwork()
    {
        Installed(Game(1091500, "Cyberpunk 2077"));

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global, new Preset { Id = "a", Name = "Anticheat" }]);

        A.CallTo(() => _presets.GetAssignmentsAsync(A<CancellationToken>._))
            .Returns<IReadOnlyDictionary<GameId, string>>(new Dictionary<GameId, string>
            {
                [new GameId("steam", "1091500")] = "a"
            });

        var library = Render<GameLibrary>();

        library.FindAll(".view-button")[(int)LibraryViewMode.Grid].Click();

        Assert.Equal("Anticheat", library.Find(".tile-tags .tag-preset").TextContent);
    }

    [Fact]
    public void LeavesAGameWithNoPresetUnbadged()
    {
        Installed(Game(1091500, "Cyberpunk 2077"));

        var library = Render<GameLibrary>();

        Assert.Empty(library.FindAll(".tag-preset"));
    }

    private static void OpenFilters(IRenderedComponent<GameLibrary> library) =>
        library.FindAll(".library-filters .dropdown > button")[1].Click();

    private static IElement Filter(IRenderedComponent<GameLibrary> library, LibraryFilter filter) =>
        library.FindAll(".library-filters .option input")[(int)filter];

    private void Installed(params GameEntry[] entries) =>
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>(entries);

    private static GameEntry Native(uint appId, string name) => Game(appId, name) with { RunsNatively = true };

    private static GameEntry Tool(uint appId, string name) => Game(appId, name) with { IsTool = true };

    private static GameEntry Game(uint appId, string name) => new()
    {
        Id = new GameId("steam", appId.ToString()),
        Name = name,
        InstallDirectory = $"/games/common/{name}",
        IsFullyInstalled = true
    };
}
