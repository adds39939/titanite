using Microsoft.AspNetCore.Components;

namespace Titanite.UI.Components.Controls;

public partial class Button : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public ButtonVariant Variant { get; set; }

    [Parameter]
    public ButtonSize Size { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool Autofocus { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    private string VariantClass => Variant switch
    {
        ButtonVariant.Primary => "button-primary",
        ButtonVariant.Destructive => "button-destructive",
        ButtonVariant.Danger => "button-danger",
        ButtonVariant.Quiet => "button-quiet",
        ButtonVariant.Bare => "button-bare",
        _ => string.Empty
    };

    private string SizeClass => Size == ButtonSize.Small ? "button-small" : string.Empty;
}
