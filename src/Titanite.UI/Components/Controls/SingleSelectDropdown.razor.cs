using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Controls;

public partial class SingleSelectDropdown : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public ButtonVariant Variant { get; set; }

    [Parameter]
    public ButtonSize Size { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private bool IsOpen { get; set; }

    private void Toggle() => IsOpen = !IsOpen;

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        StateHasChanged();
    }

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            Close();
        }
    }
}
