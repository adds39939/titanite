using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Titanite.Core.Presets;

namespace Titanite.UI.Components.Presets;

public partial class NewPresetDialog : ComponentBase
{
    [Parameter]
    public EventCallback<string> OnCreate { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private static int MaximumLength => PresetName.MaximumLength;

    private string Name { get; set; } = string.Empty;

    private bool CanCreate => PresetName.Clean(Name).Length > 0;

    private void OnNameInput(ChangeEventArgs args) =>
        Name = args.Value?.ToString() ?? string.Empty;

    private Task Create() =>
        CanCreate ? OnCreate.InvokeAsync(PresetName.Clean(Name)) : Task.CompletedTask;

    private Task Cancel() => OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) => args.Key switch
    {
        "Escape" => Cancel(),
        "Enter" => Create(),
        _ => Task.CompletedTask
    };
}
