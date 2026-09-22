using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.SettingControls;

public partial class CommandToggle : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required string Label { get; set; }

    [Parameter]
    [EditorRequired]
    public required string Command { get; set; }

    [Parameter]
    public bool IsOn { get; set; }

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public bool ShowDescription { get; set; }

    [Parameter]
    public EventCallback<bool> IsOnChanged { get; set; }

    private Task OnToggled(ChangeEventArgs args) => IsOnChanged.InvokeAsync(args.Value is true);
}
