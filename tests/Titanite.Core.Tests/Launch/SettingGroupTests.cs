using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class SettingGroupTests
{
    private static readonly SettingCategory Nvidia = new("nvidia", "Nvidia", 1);

    private static SettingDefinition Definition(string variable, string? group = null) =>
        new(variable, Nvidia, variable) { Group = group };

    [Fact]
    public void GroupsNothingIntoNothing() => Assert.Empty(SettingCatalog.Group([]));

    [Fact]
    public void LeavesAnUngroupedSectionAsOneUnnamedRun()
    {
        var groups = SettingCatalog.Group([Definition("A"), Definition("B")]);

        var group = Assert.Single(groups);

        Assert.Null(group.Name);
        Assert.Equal(["A", "B"], group.Settings.Select(setting => setting.Variable));
    }

    [Fact]
    public void StartsANewGroupWhereTheHeadingChanges()
    {
        var groups = SettingCatalog.Group([
            Definition("A", "DLSS"),
            Definition("B", "DLSS"),
            Definition("C", "NVAPI")
        ]);

        Assert.Equal(["DLSS", "NVAPI"], groups.Select(group => group.Name));
        Assert.Equal(["A", "B"], groups[0].Settings.Select(setting => setting.Variable));
        Assert.Equal(["C"], groups[1].Settings.Select(setting => setting.Variable));
    }

    [Fact]
    public void KeepsTheUngroupedRunAheadOfTheFirstHeading()
    {
        var groups = SettingCatalog.Group([Definition("A"), Definition("B", "DLSS")]);

        Assert.Equal([null, "DLSS"], groups.Select(group => group.Name));
    }

    [Fact]
    public void KeepsAHeadingUsedTwiceAsTwoRuns()
    {
        var groups = SettingCatalog.Group([
            Definition("A", "DLSS"),
            Definition("B", "NVAPI"),
            Definition("C", "DLSS")
        ]);

        Assert.Equal(["DLSS", "NVAPI", "DLSS"], groups.Select(group => group.Name));
    }

    [Fact]
    public void KeepsEverySettingInItsOriginalOrder()
    {
        SettingDefinition[] definitions =
        [
            Definition("A"),
            Definition("B", "DLSS"),
            Definition("C", "DLSS"),
            Definition("D", "NVAPI")
        ];

        Assert.Equal(
            definitions,
            SettingCatalog.Group(definitions).SelectMany(group => group.Settings));
    }

    [Fact]
    public void GroupsOneSectionAtATime()
    {
        var graphics = new SettingCategory("graphics", "Graphics", 2);

        var catalog = new SettingCatalog(
            [Nvidia, graphics],
            [
                new SettingDefinition("A", Nvidia, "A") { Group = "Shared" },
                new SettingDefinition("B", graphics, "B") { Group = "Shared" }
            ]);

        var group = Assert.Single(catalog.GroupsIn(Nvidia));

        Assert.Equal("Shared", group.Name);
        Assert.Equal(["A"], group.Settings.Select(setting => setting.Variable));
    }
}
