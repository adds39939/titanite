using Titanite.Core.Games;
using Titanite.Core.Proton;

namespace Titanite.Core.Tests.Proton;

public class ProtonCatalogueTests
{
    private static readonly GameId Overwatch = new("steam", "2357570");

    private static readonly GameId Rematch = new("steam", "2138720");

    private static ProtonBuild Build(string name, ProtonBuildKind kind = ProtonBuildKind.Valve) => new()
    {
        Name = name,
        DisplayName = name,
        InstallPath = $"/steam/{name}",
        Kind = kind
    };

    private static ProtonCatalogue Catalogue(params ProtonBuild[] builds) => new() { Builds = [.. builds] };

    private static CompatibilityToolAssignments Assignments(
        string? fallback,
        params (GameId Game, string ToolName)[] byGame) => new()
    {
        ByGame = byGame.ToDictionary(pair => pair.Game, pair => pair.ToolName),
        Default = fallback
    };

    [Fact]
    public void ResolvesAnExplicitMappingToItsBuild()
    {
        var catalogue = Catalogue(Build("proton_experimental"), Build("GE-Proton11-3", ProtonBuildKind.Custom));
        var assignments = Assignments("proton_experimental", (Overwatch, "GE-Proton11-3"));

        var selection = catalogue.Resolve(assignments.For(Overwatch), assignments.Default);

        Assert.True(selection.IsExplicit);
        Assert.Equal("GE-Proton11-3", selection.Build?.Name);
        Assert.False(selection.IsMissing);
    }

    [Fact]
    public void FallsBackToTheDefaultWithoutClaimingItIsExplicit()
    {
        var catalogue = Catalogue(Build("proton_experimental"));
        var assignments = Assignments("proton_experimental");

        var selection = catalogue.Resolve(assignments.For(Rematch), assignments.Default);

        Assert.False(selection.IsExplicit);
        Assert.Equal("proton_experimental", selection.Build?.Name);
    }

    [Fact]
    public void ReportsNothingWhenNoDefaultIsSet()
    {
        var catalogue = Catalogue(Build("proton_experimental"));

        var selection = catalogue.Resolve(null, null);

        Assert.False(selection.IsExplicit);
        Assert.Null(selection.ToolName);
        Assert.Null(selection.Build);
        Assert.False(selection.IsMissing);
    }

    [Fact]
    public void KeepsTheNameOfAnUninstalledBuild()
    {
        var catalogue = Catalogue(Build("proton_experimental"));
        var assignments = Assignments(null, (Overwatch, "GE-Proton11-3"));

        var selection = catalogue.Resolve(assignments.For(Overwatch), assignments.Default);

        Assert.True(selection.IsMissing);
        Assert.Equal("GE-Proton11-3", selection.ToolName);
        Assert.Null(selection.Build);
        Assert.Equal(["GE-Proton11-3"], catalogue.MissingToolNames(assignments));
    }

    [Fact]
    public void MatchesToolNamesIgnoringCase()
    {
        var catalogue = Catalogue(Build("GE-Proton11-3", ProtonBuildKind.Custom));
        var assignments = Assignments(null, (Overwatch, "ge-proton11-3"));

        Assert.Equal("GE-Proton11-3", catalogue.Resolve(assignments.For(Overwatch), null).Build?.Name);
        Assert.Empty(catalogue.MissingToolNames(assignments));
    }

    [Fact]
    public void ExcludesTheDefaultFromTheGamesUsingABuild()
    {
        var assignments = Assignments(
            "proton_experimental",
            (Overwatch, "GE-Proton11-3"),
            (Rematch, "GE-Proton11-3"));

        Assert.Empty(assignments.GamesUsing("proton_experimental"));

        Assert.Equal(
            [Rematch, Overwatch],
            assignments.GamesUsing("GE-Proton11-3").OrderBy(game => game.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void CountsTheDefaultAmongTheToolNamesThatMustExist()
    {
        var catalogue = Catalogue(Build("proton_experimental"));
        var assignments = Assignments("GE-Proton11-3");

        Assert.Equal(["GE-Proton11-3"], catalogue.MissingToolNames(assignments));
        Assert.Equal("proton_experimental", catalogue.Resolve("proton_experimental", null).Build?.Name);
    }
}
