using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;

namespace Titanite.UI.Components.Launch;

public partial class SaveConfirmation : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    [Parameter]
    public string Saved { get; set; } = string.Empty;

    [Parameter]
    public string? Subject { get; set; }

    [Parameter]
    public IReadOnlyList<string> AlsoChanging { get; set; } = [];

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string Title => Subject is { Length: > 0 } subject ? $"Save {subject}?" : "Save these changes?";

    private Task Confirm() => OnConfirm.InvokeAsync();

    private Task Cancel() => IsBusy ? Task.CompletedTask : OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Cancel() : Task.CompletedTask;
}
