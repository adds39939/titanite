using Microsoft.AspNetCore.Components;
using Titanite.Core.Launch;

namespace Titanite.UI.Components.SettingControls;

public partial class CommandEditor : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public required CommandDefinition Command { get; set; }

    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    [Parameter]
    public EventCallback<LaunchOptions> OptionsChanged { get; set; }

    [Parameter]
    public SettingSearch Search { get; set; } = SettingSearch.None;

    [Parameter]
    public bool ShowDescriptions { get; set; }

    private bool IsOn => Options.HasCommand(Command);

    private bool ShowsToggle => !Search.IsActive || Search.Matches(Command);

    private IEnumerable<CommandFlagGroup> ListedGroups => Search.IsActive
        ? Command.Groups
            .Select(group => group with { Flags = group.Flags.Where(Search.Matches).ToList() })
            .Where(group => group.Flags.Count > 0)
        : Command.Groups;

    private int SetCountIn(CommandFlagGroup group) =>
        group.Flags.Count(flag => Options.HasFlag(Command, flag));

    private IEnumerable<string> ChoicesFor(CommandFlagDefinition flag) =>
        Options.FindFlag(Command, flag) is { Length: > 0 } current && !flag.Choices.Contains(current)
            ? flag.Choices.Append(current)
            : flag.Choices;

    private Task OnCommandToggled(bool present) =>
        OptionsChanged.InvokeAsync(Options.WithCommand(Command, present));

    private Task ToggleFlag(CommandFlagDefinition flag, bool isOn) =>
        OptionsChanged.InvokeAsync(Options.WithSwitch(Command, flag, isOn));

    private Task SetFlag(CommandFlagDefinition flag, string? value) =>
        OptionsChanged.InvokeAsync(Options.WithFlag(Command, flag, value));
}
