using Microsoft.AspNetCore.Components;
using Titanite.Core.Games;

namespace Titanite.UI.Components.Game;

public partial class GameTags : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required GameEntry Entry { get; set; }

    [Parameter]
    public string? PresetName { get; set; }
}
