using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Abstractions.Settings;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;
using Titanite.Core.Settings;
using Titanite.UI.Components.Presets;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Tests.Components.Presets;

public sealed class PresetsPanelTests : BunitContext
{
    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private readonly ICompatibilityTools _compatibilityTools = A.Fake<ICompatibilityTools>();

    private readonly List<Preset> _stored = [Preset.Global];

    public PresetsPanelTests()
    {
        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .ReturnsLazily(() => (IReadOnlyList<Preset>)
            [
                .. _stored.OrderByDescending(preset => preset.IsGlobal).ThenBy(preset => preset.Name)
            ]);

        A.CallTo(() => _presets.GamesUsingAsync(A<string>._, A<CancellationToken>._))
            .Returns<IReadOnlyList<GameId>>([]);

        A.CallTo(() => _presets.CreateAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string name, CancellationToken _) =>
            {
                var created = new Preset { Id = $"id-{_stored.Count}", Name = name };

                _stored.Add(created);

                return created;
            });

        A.CallTo(() => _compatibilityTools.GetCatalogueAsync(A<CancellationToken>._))
            .Returns(ProtonCatalogue.Empty);

        var settings = A.Fake<IAppSettingsService>();

        A.CallTo(() => settings.GetAsync(A<CancellationToken>._)).Returns(new AppSettings());

        Services.AddSingleton(_presets);
        Services.AddSingleton(_compatibilityTools);
        Services.AddSingleton(settings);
        Services.AddSingleton(A.Fake<IGameLauncher>());
        Services.AddSingleton(Available());
        Services.AddSingleton(new SettingCatalog([], []));
        Services.AddTransient<IPresetsPresenter>(_ =>
            new PresetsPresenter(_presets, _compatibilityTools));
    }

    private static IGameLauncherAvailabilityWatcher Available()
    {
        var watcher = A.Fake<IGameLauncherAvailabilityWatcher>();

        A.CallTo(() => watcher.Current)
            .Returns(new LauncherAvailability(AvailabilityStatus.Available, null));

        return watcher;
    }

    [Fact]
    public void OpensOnGlobal()
    {
        var panel = Render<PresetsPanel>();

        Assert.Equal("Presets", panel.Find(".presets-title").TextContent);
        Assert.Equal("Global", panel.Find(".presets-actions .dropdown > button").TextContent.Trim());
    }

    [Fact]
    public void WillNotOfferToRemoveGlobal()
    {
        var panel = Render<PresetsPanel>();

        Assert.True(Remove(panel).HasAttribute("disabled"));
    }

    [Fact]
    public void PutsGlobalAtTheTopOfTheList()
    {
        _stored.Add(new Preset { Id = "a", Name = "Anticheat" });

        var panel = Render<PresetsPanel>();

        panel.Find(".presets-actions .dropdown > button").Click();

        Assert.Equal(
            ["Global", "Anticheat"],
            panel.FindAll(".presets-actions .item").Select(item => item.TextContent.Trim()));
    }

    [Fact]
    public void MakesAPresetFromTheDialog()
    {
        var panel = Render<PresetsPanel>();

        panel.FindAll(".presets-actions button")[1].Click();
        panel.Find(".name").Input("Handheld");
        panel.FindAll(".actions button")[1].Click();

        A.CallTo(() => _presets.CreateAsync("Handheld", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        Assert.Empty(panel.FindAll(".backdrop"));
        Assert.Equal("Handheld", panel.Find(".presets-actions .dropdown > button").TextContent.Trim());
    }

    [Fact]
    public void WillNotMakeAPresetWithNoName()
    {
        var panel = Render<PresetsPanel>();

        panel.FindAll(".presets-actions button")[1].Click();

        Assert.True(panel.FindAll(".actions button")[1].HasAttribute("disabled"));

        panel.Find(".name").Input("   ");

        Assert.True(panel.FindAll(".actions button")[1].HasAttribute("disabled"));
    }

    [Fact]
    public void OffersToRemoveAPresetOfItsOwn()
    {
        _stored.Add(new Preset { Id = "a", Name = "Anticheat" });

        var panel = Render<PresetsPanel>();

        panel.Find(".presets-actions .dropdown > button").Click();
        panel.FindAll(".presets-actions .item")[1].Click();

        Assert.False(Remove(panel).HasAttribute("disabled"));
    }

    [Fact]
    public void WarnsInRedBeforeTheFirstClickAndStaysRedAfterIt()
    {
        _stored.Add(new Preset { Id = "a", Name = "Anticheat" });

        var panel = Render<PresetsPanel>();

        panel.Find(".presets-actions .dropdown > button").Click();
        panel.FindAll(".presets-actions .item")[1].Click();

        Assert.Contains("button-destructive", Remove(panel).ClassList);

        Remove(panel).Click();

        Assert.Contains("button-danger", Remove(panel).ClassList);
    }

    [Fact]
    public void AsksTwiceBeforeRemoving()
    {
        _stored.Add(new Preset { Id = "a", Name = "Anticheat" });

        var panel = Render<PresetsPanel>();

        panel.Find(".presets-actions .dropdown > button").Click();
        panel.FindAll(".presets-actions .item")[1].Click();

        Remove(panel).Click();

        Assert.Equal("Confirm remove", Remove(panel).TextContent.Trim());

        A.CallTo(() => _presets.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();

        Remove(panel).Click();

        A.CallTo(() => _presets.DeleteAsync("a", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void SaysNoGameUsesAFreshPreset()
    {
        var panel = Render<PresetsPanel>();

        Assert.Contains("No game uses Global yet.", panel.Find(".preset-note").TextContent);
    }

    [Fact]
    public void SaysHowManyGamesASaveWillReach()
    {
        A.CallTo(() => _presets.GamesUsingAsync(PresetId.Global, A<CancellationToken>._))
            .Returns<IReadOnlyList<GameId>>([new GameId("steam", "620"), new GameId("steam", "400")]);

        var panel = Render<PresetsPanel>();

        Assert.Contains("2 games use Global", Collapsed(panel.Find(".preset-note").TextContent));
    }

    private static string Collapsed(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static AngleSharp.Dom.IElement Remove(IRenderedComponent<PresetsPanel> panel) =>
        panel.FindAll(".presets-actions button")[2];
}
