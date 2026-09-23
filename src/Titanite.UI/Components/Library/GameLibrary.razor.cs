using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Titanite.Core.Games;
using Titanite.Core.Settings;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Components.Library;

public partial class GameLibrary : ComponentBase, IAsyncDisposable
{
    private const float GridRowSize = 340;
    private const float ListRowSize = 74;

    private static readonly LibraryViewMode[] ViewModes = Enum.GetValues<LibraryViewMode>();
    private static readonly LibrarySortOrder[] SortOrders = Enum.GetValues<LibrarySortOrder>();
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    private string _startingSearch = string.Empty;
    private CancellationTokenSource? _pendingSearch;
    private ElementReference _grid;
    private string? _watchedGridId;
    private IJSObjectReference? _module;
    private IJSObjectReference? _gridWatcher;
    private DotNetObjectReference<GameLibrary>? _self;
    private int _columns;
    private IReadOnlyList<GameEntry>? _laidOut;
    private int _laidOutColumns;
    private GameEntry[][] _gridRows = [];
    private ICollection<GameEntry> _listItems = [];

    [Inject]
    private IGameLibraryPresenter Presenter { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private bool IsMeasuringGrid => _columns == 0;

    private ICollection<GameEntry[]> GridRows
    {
        get
        {
            LayOut();

            return _gridRows;
        }
    }

    private ICollection<GameEntry> ListItems
    {
        get
        {
            LayOut();

            return _listItems;
        }
    }

    private bool ShowsGrid =>
        !Presenter.IsLoading &&
        Presenter.LoadError is null &&
        Presenter.VisibleGames.Count > 0 &&
        Presenter.ViewMode == LibraryViewMode.Grid;

    protected override Task OnInitializedAsync()
    {
        _startingSearch = Presenter.SearchTerm;

        return Presenter.LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ShowsGrid || _grid.Id is null || _grid.Id == _watchedGridId)
        {
            return;
        }

        _watchedGridId = _grid.Id;

        await StopWatchingGridAsync();

        _module ??= await JS.InvokeAsync<IJSObjectReference?>(
            "import",
            "./Components/Library/GameLibrary.razor.js");

        if (_module is null)
        {
            return;
        }

        _self ??= DotNetObjectReference.Create(this);
        _gridWatcher = await _module.InvokeAsync<IJSObjectReference?>("watchGridColumns", _grid, _self);
    }

    [JSInvokable]
    public void SetColumns(int columns)
    {
        if (columns < 1 || columns == _columns)
        {
            return;
        }

        _columns = columns;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();

        await StopWatchingGridAsync();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _self?.Dispose();
    }

    private async Task OnSearchChanged(ChangeEventArgs args)
    {
        var term = args.Value?.ToString() ?? string.Empty;

        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = new CancellationTokenSource();

        await Task.Delay(SearchDelay, _pendingSearch.Token);

        Presenter.SearchTerm = term;
    }

    private void LayOut()
    {
        var games = Presenter.VisibleGames;
        var columns = Math.Max(_columns, 1);

        if (ReferenceEquals(games, _laidOut) && columns == _laidOutColumns)
        {
            return;
        }

        _laidOut = games;
        _laidOutColumns = columns;
        _gridRows = [.. games.Chunk(columns)];
        _listItems = games as ICollection<GameEntry> ?? [.. games];
    }

    private async Task StopWatchingGridAsync()
    {
        if (_gridWatcher is null)
        {
            return;
        }

        try
        {
            await _gridWatcher.InvokeVoidAsync("dispose");
            await _gridWatcher.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        _gridWatcher = null;
    }

    private void Select(GameEntry entry) =>
        Navigation.NavigateTo($"/library/{entry.Id.Launcher}/{entry.Id.Id}");
}
