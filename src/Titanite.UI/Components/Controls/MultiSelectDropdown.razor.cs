using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Titanite.UI.Components.Controls;

public partial class MultiSelectDropdown : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? Icon { get; set; }

    [Parameter]
    public ButtonVariant Variant { get; set; }

    [Parameter]
    public ButtonSize Size { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private bool IsOpen { get; set; }

    private string DropdownClass =>
        string.Join(
            ' ',
            new[] { "dropdown", IsOpen ? "is-open" : null, Icon is null ? null : "is-icon-only" }
                .Where(name => name is not null));

    private void Toggle() => IsOpen = !IsOpen;

    private void Close() => IsOpen = false;

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            Close();
        }
    }
}
