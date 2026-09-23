using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Abstractions.Settings;
using Titanite.Core.Games;
using Titanite.Core.Settings;
using Titanite.UI.Services.Library;

namespace Titanite.UI.Services.Presentation;

public sealed class GameLibraryPresenter(
    IGameLibrary library,
    IAppSettingsService settings,
    IPresetService presets) : IGameLibraryPresenter
{
    public IReadOnlyList<GameEntry> Games { get; private set; } = [];

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm == value)
            {
                return;
            }

            _searchTerm = value;
            Refilter();
        }
    }

    public LibraryViewMode ViewMode { get; private set; }

    public LibrarySortOrder SortOrder { get; private set; }

    public bool ShowNativeGames { get; private set; }

    public bool ShowTools { get; private set; }

    private string _searchTerm = string.Empty;

    private bool _hasReadSettings;

    private IReadOnlyDictionary<GameId, string> _appliedPresets =
        new Dictionary<GameId, string>();

    private IReadOnlyDictionary<string, string> _presetNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? PresetFor(GameEntry game) =>
        _appliedPresets.TryGetValue(game.Id, out var id) && _presetNames.TryGetValue(id, out var name)
            ? name
            : null;

    public bool IsLoading { get; private set; } = true;

    public string? LoadError { get; private set; }

    public IReadOnlyList<GameEntry> VisibleGames { get; private set; } = [];

    public int AdmittedCount { get; private set; }

    public string Subtitle => IsLoading || LoadError is not null
        ? "Games installed on this machine"
        : $"{AdmittedCount} {(AdmittedCount == 1 ? "game" : "games")} installed";

    public string EmptyMessage => Games.Count == 0
        ? "No installed games were found. Is Steam installed for this user?"
        : AdmittedCount == 0
            ? "Every installed game is hidden by the filters."
            : "No games match that search.";

    public bool IsShowing(LibraryFilter filter) => filter switch
    {
        LibraryFilter.NativeGames => ShowNativeGames,
        LibraryFilter.Tools => ShowTools,
        _ => false
    };

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasReadSettings)
        {
            var stored = await settings.GetAsync(cancellationToken);

            ViewMode = stored.LibraryView;
            SortOrder = stored.LibrarySort;
            ShowNativeGames = stored.ShowNativeGames;
            ShowTools = stored.ShowTools;

            _hasReadSettings = true;
        }

        await ReadAsync(fromDisk: false, cancellationToken);
    }

    public Task RescanAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(fromDisk: true, cancellationToken);

    private async Task ReadAsync(bool fromDisk, CancellationToken cancellationToken)
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            if (fromDisk)
            {
                library.Invalidate();
            }

            Games = await library.GetGamesAsync(cancellationToken);

            await ReadPresetsAsync(cancellationToken);
        }
        catch (Exception e)
        {
            Games = [];
            LoadError = $"Could not read the game library: {e.Message}";
        }
        finally
        {
            Refilter();
            IsLoading = false;
        }
    }

    public Task ShowAsAsync(LibraryViewMode mode, CancellationToken cancellationToken = default)
    {
        ViewMode = mode;

        return RememberAsync(stored => stored with { LibraryView = mode }, cancellationToken);
    }

    public Task SortByAsync(LibrarySortOrder order, CancellationToken cancellationToken = default)
    {
        SortOrder = order;
        Refilter();

        return RememberAsync(stored => stored with { LibrarySort = order }, cancellationToken);
    }

    public Task ShowAsync(
        LibraryFilter filter,
        bool isOn,
        CancellationToken cancellationToken = default)
    {
        switch (filter)
        {
            case LibraryFilter.NativeGames:
                ShowNativeGames = isOn;

                break;
            case LibraryFilter.Tools:
                ShowTools = isOn;

                break;
            default:
                return Task.CompletedTask;
        }

        Refilter();

        return RememberAsync(stored => filter.With(stored, isOn), cancellationToken);
    }

    private async Task ReadPresetsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _appliedPresets = await presets.GetAssignmentsAsync(cancellationToken);
            _presetNames = (await presets.GetAllAsync(cancellationToken))
                .ToDictionary(preset => preset.Id, preset => preset.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _appliedPresets = new Dictionary<GameId, string>();
            _presetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool Admits(GameEntry game) => LibraryFilters.Admits(game, ShowNativeGames, ShowTools);

    private async Task RememberAsync(
        Func<AppSettings, AppSettings> change,
        CancellationToken cancellationToken)
    {
        try
        {
            await settings.SaveAsync(change(await settings.GetAsync(cancellationToken)), cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void Refilter()
    {
        var admitted = Games.Where(Admits).ToArray();
        var term = SearchTerm.Trim();

        AdmittedCount = admitted.Length;
        VisibleGames = term.Length == 0
            ? SortOrder.Apply(admitted).ToArray()
            : SortOrder.Apply(admitted.Where(game => Matches(game, term))).ToArray();
    }

    private static bool Matches(GameEntry game, string term) =>
        game.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
        game.Id.Id.Contains(term, StringComparison.Ordinal);
}
