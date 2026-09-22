using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Controls;

public partial class MultiSelectDropdownOption : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool IsChecked { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<bool> IsCheckedChanged { get; set; }

    private Task Toggle(bool isChecked) => IsCheckedChanged.InvokeAsync(isChecked);
}
