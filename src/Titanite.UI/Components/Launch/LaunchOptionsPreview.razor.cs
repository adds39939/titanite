using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;

namespace Titanite.UI.Components.Launch;

public partial class LaunchOptionsPreview : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    [Parameter]
    public string Saved { get; set; } = string.Empty;

    [Parameter]
    public string Label { get; set; } = "Will be written as";

    private IReadOnlyList<LaunchDiffToken> Diff =>
        LaunchOptionsDiff.Compare(LaunchOptions.Parse(Saved), Options);

    private bool WritesNothing => Options.IsEmpty;
}
