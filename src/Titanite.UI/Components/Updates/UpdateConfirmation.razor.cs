using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Updates;

public partial class UpdateConfirmation : ComponentBase
{
    private const string Title = "Update and lose unsaved changes?";

    [Parameter]
    [EditorRequired]
    public required string Version { get; set; }

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private ElementReference _backdrop;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _backdrop.FocusAsync();
        }
    }

    private Task Confirm() => OnConfirm.InvokeAsync();

    private Task Cancel() => OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Cancel() : Task.CompletedTask;
}
