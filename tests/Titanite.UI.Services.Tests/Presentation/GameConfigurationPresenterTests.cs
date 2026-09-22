using Titanite.Abstractions.Desktop;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Services.Tests.Presentation;

public sealed class GameConfigurationPresenterTests
{
    private static readonly GameId Portal2 = new("steam", "620");

    private readonly ILaunchOptionsStore _launchOptions = A.Fake<ILaunchOptionsStore>();

    private readonly IGameLibrary _library = A.Fake<IGameLibrary>();

    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private readonly ICompatibilityTools _compatibilityTools = A.Fake<ICompatibilityTools>();

    private readonly IGameLauncher _launcher = A.Fake<IGameLauncher>();

    private readonly IFileManagerService _fileManager = A.Fake<IFileManagerService>();

    public GameConfigurationPresenterTests()
    {
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>([Entry]);

        A.CallTo(() => _launchOptions.GetAsync(A<GameId>._, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("MANGOHUD=1 %command%"));

        A.CallTo(() => _compatibilityTools.GetCatalogueAsync(A<CancellationToken>._))
            .Returns(ProtonCatalogue.Empty);

        A.CallTo(() => _launcher.GetCompatibilityToolAssignmentsAsync(A<CancellationToken>._))
            .Returns(CompatibilityToolAssignments.None);

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global, Handheld]);

        A.CallTo(() => _presets.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string id, CancellationToken _) =>
                PresetId.IsGlobal(id) ? GlobalPreset : Handheld);

        A.CallTo(() => _launcher.Key).Returns("steam");
        A.CallTo(() => _launcher.Name).Returns("Steam");
        A.CallTo(() => _launcher.Capabilities).Returns(new LauncherCapabilities { CanLaunchGames = true });

        Accepts(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));
    }

    private static Preset GlobalPreset => Preset.Global with
    {
        Options = LaunchOptions.Parse("PROTON_ENABLE_HDR=1 %command%")
    };

    private static Preset Handheld => new()
    {
        Id = "handheld",
        Name = "Handheld",
        Options = LaunchOptions.Parse("MANGOHUD=1 gamemoderun %command%"),
        CompatibilityTool = "GE-Proton11-3"
    };

    private static GameEntry Entry => new()
    {
        Id = Portal2,
        Name = "Portal 2",
        InstallDirectory = "/games/common/Portal 2",
        PrefixDirectory = "/games/compatdata/620/pfx"
    };

    private void Accepts(LaunchOptionsSaveResult result)
    {
        A.CallTo(() => _launchOptions.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .Returns(result);

        A.CallTo(() => _launchOptions.SaveAsync(A<GameId>._, A<LaunchOptions>._, A<CancellationToken>._))
            .Returns(result);
    }

    private void Uses(string presetId) =>
        A.CallTo(() => _presets.AppliedToAsync(Portal2, A<CancellationToken>._)).Returns(presetId);

    private GameConfigurationPresenter Create() =>
        new(_launchOptions, _library, _presets, _compatibilityTools, _launcher, _fileManager);

    private async Task<GameConfigurationPresenter> LoadedAsync()
    {
        var presenter = Create();

        await presenter.LoadAsync(Portal2);

        return presenter;
    }

    [Fact]
    public async Task ReadsWhatIsStoredForTheGame()
    {
        var presenter = await LoadedAsync();

        Assert.Equal("Portal 2", presenter.Entry?.Name);
        Assert.Equal("MANGOHUD=1 %command%", presenter.Editing.Format());
        Assert.False(presenter.IsLoading);
        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task ReportsAGameThatIsNotInstalled()
    {
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>([]);

        var presenter = await LoadedAsync();

        Assert.Null(presenter.Entry);
        Assert.Null(presenter.LoadError);
    }

    [Fact]
    public async Task ExplainsAFailedRead()
    {
        A.CallTo(() => _launchOptions.GetAsync(A<GameId>._, A<CancellationToken>._))
            .Throws(new IOException("localconfig.vdf is locked"));

        var presenter = await LoadedAsync();

        Assert.Contains("localconfig.vdf is locked", presenter.LoadError);
        Assert.True(presenter.Editing.IsEmpty);
    }

    [Fact]
    public async Task NoticesAnEdit()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.True(presenter.HasChanges);
    }

    [Fact]
    public async Task EditingDropsThePresetTheGameHad()
    {
        Uses(PresetId.Global);

        var presenter = await LoadedAsync();

        Assert.Equal(PresetId.Global, presenter.AppliedPresetId);

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.Null(presenter.AppliedPresetId);
        Assert.Equal(IGameConfigurationPresenter.NoPreset, presenter.PresetLabel);
    }

    [Fact]
    public async Task ChoosingAProtonBuildDropsThePresetToo()
    {
        Uses(PresetId.Global);

        var presenter = await LoadedAsync();

        presenter.ChooseCompatibilityTool("GE-Proton11-3");

        Assert.Null(presenter.AppliedPresetId);
    }

    [Fact]
    public async Task TakingAPresetReplacesWhatIsBeingEdited()
    {
        var presenter = await LoadedAsync();

        await presenter.UsePresetAsync(PresetId.Global);

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", presenter.Editing.Format());
        Assert.Equal(PresetId.Global, presenter.AppliedPresetId);
        Assert.Equal("Global", presenter.PresetLabel);
    }

    [Fact]
    public async Task TakingAPresetTakesItsProtonBuildAsWell()
    {
        var presenter = await LoadedAsync();

        await presenter.UsePresetAsync(Handheld.Id);

        Assert.Equal("GE-Proton11-3", presenter.CompatTool);
        Assert.Equal("Handheld", presenter.PresetLabel);
    }

    [Fact]
    public async Task OffersGlobalAheadOfEveryOtherPreset()
    {
        var presenter = await LoadedAsync();

        Assert.Equal(["Global", "Handheld"], presenter.Presets.Select(preset => preset.Name));
    }

    [Fact]
    public async Task RemembersThePresetOnSave()
    {
        var presenter = await LoadedAsync();

        await presenter.UsePresetAsync(Handheld.Id);
        await presenter.SaveAsync();

        A.CallTo(() => _presets.ApplyAsync(Portal2, Handheld.Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task DroppingAPresetIsAChangeWorthSaving()
    {
        Uses(PresetId.Global);

        var presenter = await LoadedAsync();

        await presenter.UsePresetAsync(null);

        Assert.True(presenter.HasChanges);
        Assert.Contains(presenter.PendingSideEffects, change => change.StartsWith("Stop using a preset"));
    }

    [Fact]
    public async Task RevertingGoesBackToWhatWasStored()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));
        presenter.Revert();

        Assert.Equal("MANGOHUD=1 %command%", presenter.Editing.Format());
        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task SavingClearsTheUnsavedState()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        await presenter.SaveAsync();

        Assert.False(presenter.HasChanges);
        Assert.Equal(StatusTone.Success, presenter.Status?.Tone);
    }

    [Fact]
    public async Task ARefusedSaveKeepsTheChangesAndSaysWhy()
    {
        Accepts(new LaunchOptionsSaveResult(
            LaunchOptionsSaveStatus.LauncherUnavailable,
            "Steam is not running."));

        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        await presenter.SaveAsync();

        Assert.True(presenter.HasChanges);
        Assert.Equal(StatusTone.Error, presenter.Status?.Tone);
        Assert.Equal("Steam is not running.", presenter.Status?.Text);
    }

    [Fact]
    public async Task SendsTheCompatibilityToolOnlyWhenItChanged()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        await presenter.SaveAsync();

        A.CallTo(() => _launchOptions.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>.That.IsEmpty(),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        presenter.ChooseCompatibilityTool("GE-Proton11-3");

        await presenter.SaveAsync();

        A.CallTo(() => _launchOptions.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>.That.Matches(
                    tools => tools.Contains(
                        new KeyValuePair<GameId, string>(Portal2, "GE-Proton11-3"))),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ResettingEmptiesTheGameAndDropsItsPreset()
    {
        var presenter = await LoadedAsync();

        await presenter.ResetAsync();

        Assert.True(presenter.Editing.IsEmpty);
        Assert.False(presenter.HasAnythingToReset);
        A.CallTo(() => _presets.ApplyAsync(Portal2, null, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CopyingFromAGameOnAPresetTakesThePresetItself()
    {
        var other = new GameEntry
        {
            Id = new GameId("steam", "400"),
            Name = "Portal",
            InstallDirectory = "/games/common/Portal"
        };

        A.CallTo(() => _presets.AppliedToAsync(other.Id, A<CancellationToken>._)).Returns(Handheld.Id);

        var presenter = await LoadedAsync();

        await presenter.CopyFromAsync(other);

        Assert.Equal(Handheld.Id, presenter.AppliedPresetId);
        Assert.Equal("MANGOHUD=1 gamemoderun %command%", presenter.Editing.Format());
        Assert.Equal("GE-Proton11-3", presenter.CompatTool);
        Assert.Contains("uses Handheld", presenter.Status?.Text);
    }

    [Fact]
    public async Task SavingACopiedPresetLinksThisGameToItToo()
    {
        var other = new GameEntry
        {
            Id = new GameId("steam", "400"),
            Name = "Portal",
            InstallDirectory = "/games/common/Portal"
        };

        A.CallTo(() => _presets.AppliedToAsync(other.Id, A<CancellationToken>._)).Returns(Handheld.Id);

        var presenter = await LoadedAsync();

        await presenter.CopyFromAsync(other);
        await presenter.SaveAsync();

        A.CallTo(() => _presets.ApplyAsync(Portal2, Handheld.Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CopyingFromAGameOnNoPresetLeavesThisOneOnNone()
    {
        var other = new GameEntry
        {
            Id = new GameId("steam", "400"),
            Name = "Portal",
            InstallDirectory = "/games/common/Portal"
        };

        Uses(PresetId.Global);

        A.CallTo(() => _launchOptions.GetAsync(other.Id, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=fps %command%"));

        var presenter = await LoadedAsync();

        await presenter.CopyFromAsync(other);

        Assert.Null(presenter.AppliedPresetId);
        Assert.Equal("DXVK_HUD=fps %command%", presenter.Editing.Format());
    }

    [Fact]
    public async Task IgnoresAPresetTheSourceNamesThatIsNoLongerThere()
    {
        var other = new GameEntry
        {
            Id = new GameId("steam", "400"),
            Name = "Portal",
            InstallDirectory = "/games/common/Portal"
        };

        A.CallTo(() => _presets.AppliedToAsync(other.Id, A<CancellationToken>._)).Returns("removed");

        A.CallTo(() => _launchOptions.GetAsync(other.Id, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=fps %command%"));

        var presenter = await LoadedAsync();

        await presenter.CopyFromAsync(other);

        Assert.Null(presenter.AppliedPresetId);
        Assert.Equal("DXVK_HUD=fps %command%", presenter.Editing.Format());
    }

    [Fact]
    public async Task CopyingFromAnotherGameWritesNothingYet()
    {
        var other = new GameEntry
        {
            Id = new GameId("steam", "400"),
            Name = "Portal",
            InstallDirectory = "/games/common/Portal"
        };

        A.CallTo(() => _launchOptions.GetAsync(other.Id, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=fps %command%"));

        var presenter = await LoadedAsync();

        await presenter.CopyFromAsync(other);

        Assert.Equal("DXVK_HUD=fps %command%", presenter.Editing.Format());
        Assert.Contains("Portal", presenter.Status?.Text);

        A.CallTo(() => _launchOptions.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SaysNothingChangedWhenTheStoredSettingsStandStill()
    {
        var presenter = await LoadedAsync();

        Assert.False(await presenter.RefreshAsync());
        Assert.Null(presenter.Status);
    }

    [Fact]
    public async Task TakesOnLaunchOptionsThatChangedOutsideTheApp()
    {
        var presenter = await LoadedAsync();

        A.CallTo(() => _launchOptions.GetAsync(Portal2, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.True(await presenter.RefreshAsync());

        Assert.Equal("DXVK_HUD=1 %command%", presenter.Editing.Format());
        Assert.False(presenter.HasChanges);
        Assert.Contains("changed outside Titanite", presenter.Status?.Text);
    }

    [Fact]
    public async Task TakesOnAProtonBuildThatChangedOutsideTheApp()
    {
        var presenter = await LoadedAsync();

        A.CallTo(() => _launcher.GetCompatibilityToolAssignmentsAsync(A<CancellationToken>._))
            .Returns(new CompatibilityToolAssignments
            {
                ByGame = new Dictionary<GameId, string> { [Portal2] = "GE-Proton11-3" }
            });

        Assert.True(await presenter.RefreshAsync());

        Assert.Equal("GE-Proton11-3", presenter.CompatTool);
        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task DropsThePresetWhenTheGameIsChangedOutsideTheApp()
    {
        Uses(PresetId.Global);

        var presenter = await LoadedAsync();

        A.CallTo(() => _launchOptions.GetAsync(Portal2, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        A.CallTo(() => _presets.AppliedToAsync(Portal2, A<CancellationToken>._)).Returns((string?)null);

        Assert.True(await presenter.RefreshAsync());

        Assert.Null(presenter.AppliedPresetId);
        Assert.False(presenter.HasChanges);
    }

    [Fact]
    public async Task KeepsUnsavedEditsWhenTheGameChangesUnderneathThem()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 %command%"));

        A.CallTo(() => _launchOptions.GetAsync(Portal2, A<CancellationToken>._))
            .Returns(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.True(await presenter.RefreshAsync());

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", presenter.Editing.Format());
        Assert.True(presenter.HasChanges);
        Assert.Equal(StatusTone.Warning, presenter.Status?.Tone);
        Assert.Contains("unsaved changes", presenter.Status?.Text);
    }

    [Fact]
    public async Task WillNotRefreshWhileASaveIsInFlight()
    {
        var saving = new TaskCompletionSource<LaunchOptionsSaveResult>();

        A.CallTo(() => _launchOptions.SaveManyAsync(
                A<IReadOnlyDictionary<GameId, LaunchOptions>>._,
                A<IReadOnlyDictionary<GameId, string>>._,
                A<CancellationToken>._))
            .Returns(saving.Task);

        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 %command%"));

        var save = presenter.SaveAsync();

        Assert.False(await presenter.RefreshAsync());

        saving.SetResult(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));

        await save;
    }

    [Fact]
    public async Task WarnsOnceBeforeLaunchingOverUnsavedChanges()
    {
        var presenter = await LoadedAsync();

        presenter.Edit(LaunchOptions.Parse("DXVK_HUD=1 %command%"));

        Assert.True(presenter.WarnBeforeLaunching());
        Assert.Equal(StatusTone.Warning, presenter.Status?.Tone);

        A.CallTo(() => _launcher.Launch(A<GameId>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task LaunchesWithoutWarningWhenNothingIsUnsaved()
    {
        var presenter = await LoadedAsync();

        Assert.False(presenter.WarnBeforeLaunching());

        A.CallTo(() => _launcher.Launch(Portal2)).Returns(true);

        presenter.Launch();

        Assert.Equal(StatusTone.Success, presenter.Status?.Tone);
        Assert.Contains("Steam", presenter.Status?.Text);
    }

    [Fact]
    public void NamesTheLauncherBesideTheIdentifier() =>
        Assert.Equal("Steam · 620", Create().Describe(Portal2));

    [Fact]
    public void FallsBackToTheRawKeyForALauncherItDoesNotKnow() =>
        Assert.Equal("gog · 1207666283", Create().Describe(new GameId("gog", "1207666283")));

    [Fact]
    public void DescribesNothingWhenThereIsNoGame() =>
        Assert.Equal(string.Empty, Create().Describe(default));

    [Fact]
    public async Task SaysWhenAFolderIsNotThere()
    {
        A.CallTo(() => _fileManager.OpenDirectory(A<string>._)).Returns(DirectoryOpenStatus.NotFound);

        var presenter = await LoadedAsync();

        presenter.OpenPrefixDirectory();

        Assert.Equal(StatusTone.Error, presenter.Status?.Tone);
        Assert.Contains("Wine prefix", presenter.Status?.Text);
    }

    [Fact]
    public async Task SaysNothingWhenAFolderOpens()
    {
        A.CallTo(() => _fileManager.OpenDirectory(A<string>._)).Returns(DirectoryOpenStatus.Opened);

        var presenter = await LoadedAsync();

        presenter.OpenInstallDirectory();

        Assert.Null(presenter.Status);
    }
}
