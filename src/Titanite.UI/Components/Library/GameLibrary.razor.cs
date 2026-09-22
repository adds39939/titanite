using Microsoft.AspNetCore.Components;
using Titanite.Core.Games;
using Titanite.Core.Settings;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Components.Library;

public partial class GameLibrary : ComponentBase
{
    private static readonly LibraryViewMode[] ViewModes = Enum.GetValues<LibraryViewMode>();

    private static readonly LibrarySortOrder[] SortOrders = Enum.GetValues<LibrarySortOrder>();

    [Inject]
    private IGameLibraryPresenter Presenter { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override Task OnInitializedAsync() => Presenter.LoadAsync();

    private void OnSearchChanged(ChangeEventArgs args) =>
        Presenter.SearchTerm = args.Value?.ToString() ?? string.Empty;

    private void Select(GameEntry entry) =>
        Navigation.NavigateTo($"/library/{entry.Id.Launcher}/{entry.Id.Id}");
}
