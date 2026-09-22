using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;

namespace Titanite.UI.Components.SettingControls;

public partial class SettingEditor : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required SettingDefinition Definition { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public bool IsIgnored { get; set; }

    [Parameter]
    public string? BuildName { get; set; }

    [Parameter]
    public bool SetOnly { get; set; }

    [Parameter]
    public bool ShowDescription { get; set; }

    private bool IsOn => Definition.IsOn(Value);

    private IEnumerable<string> Choices =>
        Value is { Length: > 0 } current && !Definition.Choices.Contains(current)
            ? Definition.Choices.Append(current)
            : Definition.Choices;

    private Task OnToggled(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value is true ? Definition.OnValue : null);

    private Task OnChoicePicked(ChangeEventArgs args) => Apply(args.Value?.ToString());

    private Task OnTextChanged(ChangeEventArgs args) => Apply(args.Value?.ToString());

    private Task OnEmptiableTextChanged(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value?.ToString()?.Trim() ?? string.Empty);

    private Task SetEmpty() => ValueChanged.InvokeAsync(string.Empty);

    private Task Remove() => ValueChanged.InvokeAsync(null);

    private Task Apply(string? value) =>
        ValueChanged.InvokeAsync(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
}
