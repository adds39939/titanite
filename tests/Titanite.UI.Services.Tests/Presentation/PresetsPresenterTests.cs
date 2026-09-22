using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Services.Tests.Presentation;

public sealed class PresetsPresenterTests
{
    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private readonly ICompatibilityTools _compatibilityTools = A.Fake<ICompatibilityTools>();

    private readonly List<Preset> _stored = [Preset.Global];

    private readonly Dictionary<string, List<GameId>> _using = [];

    public PresetsPresenterTests()
    {
        A.CallTo(() => _compatibilityTools.GetCatalogueAsync(A<CancellationToken>._))
            .Returns(ProtonCatalogue.Empty);

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .ReturnsLazily(() => (IReadOnlyList<Preset>)
            [
                .. _stored.OrderByDescending(preset => preset.IsGlobal).ThenBy(preset => preset.Name)
            ]);

        A.CallTo(() => _presets.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string id, CancellationToken _) =>
                _stored.FirstOrDefault(preset => PresetId.Same(preset.Id, id)) ?? Preset.Global);

        A.CallTo(() => _presets.GamesUsingAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string id, CancellationToken _) =>
                (IReadOnlyList<GameId>)[.. _using.GetValueOrDefault(id, [])]);

        A.CallTo(() => _presets.CreateAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string name, CancellationToken _) =>
            {
                var created = new Preset { Id = $"id-{_stored.Count}", Name = name };

                _stored.Add(created);

                return created;
            });

        A.CallTo(() => _presets.DeleteAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string id, CancellationToken _) =>
                _stored.RemoveAll(preset => PresetId.Same(preset.Id, id)));

        A.CallTo(() => _presets.SaveAndApplyAsync(A<Preset>._, A<CancellationToken>._))
            .ReturnsLazily((Preset preset, CancellationToken _) =>
            {
                Replace(preset);

                return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
            });
    }

    private void Replace(Preset preset)
    {
        _stored.RemoveAll(candidate => PresetId.Same(candidate.Id, preset.Id));
        _stored.Add(preset);
    }

    private async Task<PresetsPresenter> LoadedAsync()
    {
        var presenter = new PresetsPresenter(_presets, _compatibilityTools);

        await presenter.LoadAsync();

        return presenter;
    }

    [Fact]
    public async Task OpensOnGlobal()
    {
        var presenter = await LoadedAsync();

        Assert.Equal(PresetId.Global, presenter.SelectedId);
        Assert.Equal("Global", presenter.Selected.Name);
        Assert.False(presenter.IsLoading);
    }

    [Fact]
    public async Task WillNotOfferToRemoveGlobal()
    {
        var presenter = await LoadedAsync();

        Assert.False(presenter.CanDelete);
    }

    [Fact]
    public async Task OffersToRemoveAPresetOfItsOwn()
    {
        var presenter = await LoadedAsync();

        await presenter.CreateAsync("Handheld");

        Assert.True(presenter.CanDelete);
    }

    [Fact]
    public async Task OpensTheNewPresetAfterMakingIt()
    {
        var presenter = await LoadedAsync();

        await presenter.CreateAsync("Handheld");

        Assert.Equal("Handheld", presenter.Selected.Name);
        Assert.True(presenter.Editing.IsEmpty);
        Assert.Equal(StatusTone.Success, presenter.Status?.Tone);
    }

    [Fact]
    public async Task GoesBackToGlobalAfterRemovingOne()
    {
        var presenter = await LoadedAsync();

        await presenter.CreateAsync("Handheld");
        await presenter.DeleteAsync();

        Assert.Equal(PresetId.Global, presenter.SelectedId);
    }

    [Fact]
    public async Task SwapsWhatIsBeingEditedWhenAnotherPresetIsPicked()
    {
        _stored.Add(new Preset
        {
            Id = "handheld",
            Name = "Handheld",
            Options = LaunchOptions.Parse("MANGOHUD=1 %command%"),
            CompatibilityTool = "GE-Proton11-3"
        });

        var presenter = await LoadedAsync();

        await presenter.SelectAsync("handheld");

        Assert.Equal("MANGOHUD=1 %command%", presenter.Editing.Format());
        Assert.Equal("GE-Proton11-3", presenter.CompatTool);
        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task NoticesAnEditAndTakesItBackOnRevert()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.True(presenter.HasChanges);

        presenter.Revert();

        Assert.False(presenter.HasChanges);
        Assert.True(presenter.Editing.IsEmpty);
    }

    [Fact]
    public async Task CountsAProtonBuildAsAChange()
    {
        var presenter = await LoadedAsync();

        presenter.ChooseCompatibilityTool("GE-Proton11-3");

        Assert.True(presenter.HasChanges);
        Assert.True(presenter.CompatToolChanged);
    }

    [Fact]
    public async Task SavesThroughTheServiceAndSettles()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));
        presenter.ChooseCompatibilityTool("GE-Proton11-3");

        await presenter.SaveAsync();

        A.CallTo(() => _presets.SaveAndApplyAsync(
                A<Preset>.That.Matches(preset =>
                    preset.Id == PresetId.Global &&
                    preset.Options.Format() == "DXVK_HUD=1 %command%" &&
                    preset.CompatibilityTool == "GE-Proton11-3"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        Assert.False(presenter.HasChanges);
        Assert.Equal(StatusTone.Success, presenter.Status?.Tone);
    }

    [Fact]
    public async Task KeepsTheChangesWhenASaveIsRefused()
    {
        A.CallTo(() => _presets.SaveAndApplyAsync(A<Preset>._, A<CancellationToken>._))
            .Returns(new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.LauncherUnavailable,
                "Steam is not running."));

        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        await presenter.SaveAsync();

        Assert.True(presenter.HasChanges);
        Assert.Equal("Steam is not running.", presenter.Status?.Text);
    }

    [Fact]
    public async Task SaysHowManyGamesASaveWillReach()
    {
        _using[PresetId.Global] = [new GameId("steam", "620"), new GameId("steam", "400")];

        var presenter = await LoadedAsync();

        Assert.Equal(2, presenter.AppliedCount);
        Assert.Contains(
            presenter.PendingSideEffects,
            change => change.Contains("2 games using this preset"));
    }

    [Fact]
    public async Task SaysWhatAProtonBuildWillDoToTheGamesUsingIt()
    {
        var presenter = await LoadedAsync();

        presenter.ChooseCompatibilityTool("GE-Proton11-3");

        Assert.Contains(
            presenter.PendingSideEffects,
            change => change.Contains("GE-Proton11-3"));
    }

    [Fact]
    public async Task ResetsThePresetAndReportsIt()
    {
        _stored.Clear();
        _stored.Add(Preset.Global with { Options = LaunchOptions.Parse("MANGOHUD=1 %command%") });

        A.CallTo(() => _presets.ResetAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => Replace(Preset.Global));

        var presenter = await LoadedAsync();

        Assert.True(presenter.HasAnythingToReset);

        await presenter.ResetAsync();

        Assert.True(presenter.Editing.IsEmpty);
        Assert.False(presenter.HasAnythingToReset);
    }

    [Fact]
    public async Task ExplainsAFailedRead()
    {
        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Throws(new IOException("presets.json is locked"));

        var presenter = await LoadedAsync();

        Assert.Contains("presets.json is locked", presenter.Status?.Text);
        Assert.False(presenter.IsLoading);
    }
}
