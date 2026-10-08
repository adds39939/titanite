using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Titanite.Core.Presets;

namespace Titanite.UI.Components.Presets;

public partial class PresetNameDialog : ComponentBase
{
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string Note { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string ConfirmLabel { get; set; } = string.Empty;

    [Parameter]
    public string InitialName { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private static int MaximumLength => PresetName.MaximumLength;

    private string Name { get; set; } = string.Empty;

    private bool CanConfirm =>
        PresetName.Clean(Name) is { Length: > 0 } clean &&
        !string.Equals(clean, InitialName, StringComparison.Ordinal);

    protected override void OnInitialized() => Name = InitialName;

    private void OnNameInput(ChangeEventArgs args) =>
        Name = args.Value?.ToString() ?? string.Empty;

    private Task Confirm() =>
        CanConfirm ? OnConfirm.InvokeAsync(PresetName.Clean(Name)) : Task.CompletedTask;

    private Task Cancel() => OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) => args.Key switch
    {
        "Escape" => Cancel(),
        "Enter" => Confirm(),
        _ => Task.CompletedTask
    };
}
