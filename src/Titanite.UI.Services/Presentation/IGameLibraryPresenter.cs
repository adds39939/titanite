using Titanite.Core.Games;
using Titanite.Core.Settings;

namespace Titanite.UI.Services.Presentation;

public interface IGameLibraryPresenter
{
    IReadOnlyList<GameEntry> Games { get; }

    string SearchTerm { get; set; }

    LibraryViewMode ViewMode { get; }

    LibrarySortOrder SortOrder { get; }

    bool ShowNativeGames { get; }

    bool ShowTools { get; }

    bool IsLoading { get; }

    string? LoadError { get; }

    IReadOnlyList<GameEntry> VisibleGames { get; }

    int AdmittedCount { get; }

    string Subtitle { get; }

    string EmptyMessage { get; }

    string? PresetFor(GameEntry game);

    bool IsShowing(LibraryFilter filter);

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task RescanAsync(CancellationToken cancellationToken = default);

    Task ShowAsAsync(LibraryViewMode mode, CancellationToken cancellationToken = default);

    Task SortByAsync(LibrarySortOrder order, CancellationToken cancellationToken = default);

    Task ShowAsync(LibraryFilter filter, bool isOn, CancellationToken cancellationToken = default);
}
