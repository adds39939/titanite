using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Proton;

namespace Titanite.UI.Components.Proton;

public partial class ProtonPanel : ComponentBase
{
    [Inject]
    private ICompatibilityTools Tools { get; set; } = null!;

    [Inject]
    private IGameLibrary Library { get; set; } = null!;

    [Inject]
    private IGameLauncher Launcher { get; set; } = null!;

    [Inject]
    private SettingCatalog Catalog { get; set; } = null!;

    private ProtonCatalogue Catalogue { get; set; } = ProtonCatalogue.Empty;

    private CompatibilityToolAssignments Assignments { get; set; } = CompatibilityToolAssignments.None;

    private IReadOnlyDictionary<GameId, string> InstalledNames { get; set; } = new Dictionary<GameId, string>();

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Catalogue = await Tools.GetCatalogueAsync();
            Assignments = await Launcher.GetCompatibilityToolAssignmentsAsync();

            InstalledNames = (await Library.GetGamesAsync())
                .ToDictionary(entry => entry.Id, entry => entry.Name);
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

    private ProtonSelection DefaultSelection => Catalogue.Resolve(null, Assignments.Default);

    private IReadOnlyList<string> MissingToolNames => Catalogue.MissingToolNames(Assignments);

    private string DefaultSummary => DefaultSelection switch
    {
        { Build: { } build } =>
            $"Games without a choice of their own use {build.DisplayName}, unless Steam names a " +
            "different build for them.",
        { ToolName: { } toolName } =>
            $"The default is set to “{toolName}”, which is not installed.",
        _ => "No default build is set, so Steam decides for each game."
    };

    private static string KindLabel(ProtonBuild build) => build.Kind switch
    {
        ProtonBuildKind.Valve => "Valve",
        ProtonBuildKind.Custom => "Community",
        _ => "Unknown"
    };

    private bool IsDefault(ProtonBuild build) =>
        string.Equals(Assignments.Default, build.Name, StringComparison.OrdinalIgnoreCase);

    private string GamesSummary(ProtonBuild build)
    {
        var games = NamesFor(Assignments.GamesUsing(build.Name));

        return games.Count == 0
            ? "No game is pointed at this build on its own."
            : $"Used by {string.Join(", ", games)}.";
    }

    private string SupportSummary(ProtonBuild build)
    {
        if (!build.Capabilities.IsKnown)
        {
            return "Which settings it reads could not be checked.";
        }

        var ignored = Catalog.All
            .Where(definition => build.Capabilities.Ignores(definition.Variable))
            .Select(definition => definition.Label)
            .ToList();

        return ignored.Count == 0
            ? "Reads every Proton setting Titanite offers."
            : $"Ignores {string.Join(", ", ignored)}.";
    }

    private string MissingSummary(string toolName)
    {
        var games = NamesFor(Assignments.GamesUsing(toolName));

        if (string.Equals(Assignments.Default, toolName, StringComparison.OrdinalIgnoreCase))
        {
            games.Insert(0, "set as the default");
        }

        return games.Count == 0 ? "mapped, but nothing uses it" : string.Join(", ", games);
    }

    private List<string> NamesFor(IEnumerable<GameId> ids) =>
        [
            .. ids
                .Select(id => InstalledNames.GetValueOrDefault(id, $"game {id.Id}"))
                .Order(StringComparer.CurrentCultureIgnoreCase)
        ];
}
