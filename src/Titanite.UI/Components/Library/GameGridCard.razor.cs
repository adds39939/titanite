using Microsoft.AspNetCore.Components;
using Titanite.Core.Games;

namespace Titanite.UI.Components.Library;

public partial class GameGridCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required GameEntry Entry { get; set; }

    [Parameter]
    public string? PresetName { get; set; }

    [Parameter]
    public EventCallback<GameEntry> OnSelect { get; set; }

    private GameEntry? _shownEntry;

    private string? _shownPresetName;

    private bool _hasChanged;

    protected override void OnParametersSet()
    {
        _hasChanged = !Equals(_shownEntry, Entry) || _shownPresetName != PresetName;
        _shownEntry = Entry;
        _shownPresetName = PresetName;
    }

    protected override bool ShouldRender() => _hasChanged;

    private Task Select() => OnSelect.InvokeAsync(Entry);
}
