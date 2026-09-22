using Microsoft.AspNetCore.Components;
using Titanite.Core.Games;

namespace Titanite.UI.Pages;

public partial class Game : ComponentBase
{
    [Parameter]
    public string Launcher { get; set; } = string.Empty;

    [Parameter]
    public string Id { get; set; } = string.Empty;

    private GameId GameId => new(Launcher, Id);
}
