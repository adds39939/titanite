using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Controls;

public partial class CollapsibleGroup : ComponentBase
{
    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public int SetCount { get; set; }

    [Parameter]
    [EditorRequired]
    public required RenderFragment ChildContent { get; set; }
}
