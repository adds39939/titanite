using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Controls;

public partial class SingleSelectDropdownItem : ComponentBase
{
    [CascadingParameter]
    private SingleSelectDropdown? Menu { get; set; }

    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool? IsSelected { get; set; }

    [Parameter]
    public EventCallback OnSelect { get; set; }

    private string ItemClass => IsSelected == true ? "item is-selected" : "item";

    private string Role => IsSelected.HasValue ? "menuitemradio" : "menuitem";

    private string? Checked => IsSelected.HasValue
        ? IsSelected == true ? "true" : "false"
        : null;

    private async Task Select()
    {
        Menu?.Close();

        await OnSelect.InvokeAsync();
    }
}
