using Microsoft.AspNetCore.Components;
using Titanite.Core.Games;

namespace Titanite.UI.Components.Library;

public partial class GameGridCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required GameEntry Entry { get; set; }

    [Parameter]
    public string? PresetName { get; set; }

    [Parameter]
    public EventCallback<GameEntry> OnSelect { get; set; }

    private Task Select() => OnSelect.InvokeAsync(Entry);
}
