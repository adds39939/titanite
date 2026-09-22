using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;

namespace Titanite.UI.Components.SettingControls;

public partial class CompoundEditor : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required CompoundSchema Schema { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public bool SetOnly { get; set; }

    [Parameter]
    public bool ShowDescriptions { get; set; }

    private CompoundValue Current => CompoundValue.Parse(Schema, Value);

    private IEnumerable<CompoundOptionGroup> ListedGroups =>
        SetOnly ? Current.GroupsWithValues() : Schema.Groups;

    private bool ShowsAdditional => !SetOnly || AdditionalCount > 0;

    private string AdditionalOptions =>
        string.Join(Schema.Separator, Current.Unrecognised.Select(entry => entry.Render(Schema)));

    private int SetCountIn(CompoundOptionGroup group) =>
        group.Options.Count(option => Current.Contains(option.Key));

    private int AdditionalCount => Current.Unrecognised.Count;

    private string AdditionalPlaceholder =>
        string.Join(Schema.Separator, "round_corners" + Schema.Assignment + "5", "engine_version");

    private IEnumerable<string> ChoicesFor(CompoundOptionDefinition option) =>
        Current.GetValue(option.Key) is { Length: > 0 } current && !option.Choices.Contains(current)
            ? option.Choices.Append(current)
            : option.Choices;

    private Task ToggleOption(string key, bool isOn) =>
        Publish(isOn ? Current.Set(key, null) : Current.Remove(key));

    private Task SetOption(string key, string? value) =>
        Publish(string.IsNullOrWhiteSpace(value) ? Current.Remove(key) : Current.Set(key, value.Trim()));

    private Task OnAdditionalChanged(ChangeEventArgs args) =>
        Publish(Current.ReplaceUnrecognised(CompoundValue.Parse(Schema, args.Value?.ToString()).Entries));

    private Task Publish(CompoundValue value) =>
        ValueChanged.InvokeAsync(value.IsEmpty ? null : value.Format());
}
