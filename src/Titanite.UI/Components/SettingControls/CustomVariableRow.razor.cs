using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.SettingControls;

public partial class CustomVariableRow : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required string Name { get; set; }

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public EventCallback OnRemove { get; set; }
}
