using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Proton;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Components.SettingControls;

public partial class ProtonVersionEditor : ComponentBase
{
    public const string InheritValue = CompatibilityTool.Inherit;

    [Inject]
    private ICompatibilityTools Tools { get; set; } = null!;

    [Inject]
    private IGameLauncher Launcher { get; set; } = null!;

    [Parameter]
    public GameId? GameId { get; set; }

    [Parameter]
    [EditorRequired]
    public required string Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    private ProtonCatalogue Catalogue { get; set; } = ProtonCatalogue.Empty;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private CompatibilityToolAssignments Assignments { get; set; } = CompatibilityToolAssignments.None;

    private bool ForGame => GameId is not null;

    private ProtonSelection Stored => Catalogue.Resolve(
        GameId is { } id ? Assignments.For(id) : null,
        Assignments.Default);

    private ProtonSelection DefaultSelection => Catalogue.Resolve(null, Assignments.Default);

    private ProtonBuild? Chosen => Catalogue.FindBuild(Value);

    private bool IsInherited => Value.Length == 0;

    private bool ChoiceIsMissing => !IsInherited && Chosen is null;

    private string StoredValue => Stored.IsExplicit ? Stored.ToolName ?? InheritValue : InheritValue;

    private bool HasPendingChange =>
        ForGame && !string.Equals(Value, StoredValue, StringComparison.OrdinalIgnoreCase);

    private string StoredName => Stored.Build?.DisplayName ?? Stored.ToolName ?? "whatever Steam chose";

    private string StoredSummary => Stored switch
    {
        { IsExplicit: true, Build: { } build } => $"Currently set to {build.DisplayName}.",
        { IsExplicit: true, ToolName: { } toolName } => $"Currently set to {toolName}, which is not installed.",
        { Build: { } build } => $"No choice of its own, so Steam decides — normally {build.DisplayName}.",
        _ => "No choice of its own, and no default is set, so Steam decides."
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Catalogue = await Tools.GetCatalogueAsync();
            Assignments = await Launcher.GetCompatibilityToolAssignmentsAsync();
        }
        catch (Exception e)
        {
            LoadError = $"Could not read the installed Proton builds: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string InheritLabel => ForGame
        ? DefaultSelection.Build is { } build
            ? $"Let Steam decide — usually {build.DisplayName}"
            : "Let Steam decide"
        : "Leave the build alone";

    private static string OptionLabel(ProtonBuild build) =>
        build.Kind == ProtonBuildKind.Valve ? build.DisplayName : $"{build.DisplayName} (community)";

    private Task OnSelected(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value?.ToString() ?? InheritValue);
}
