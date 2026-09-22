using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class SettingSearchTests
{
    private static readonly SettingCategory Display = new("display", "Display", 4);

    private static readonly SettingDefinition Wayland =
        new("PROTON_ENABLE_WAYLAND", Display, "Run natively on Wayland")
        {
            Description = "Skips XWayland. Required before HDR will do anything."
        };

    private static readonly CommandFlagDefinition HdrFlag =
        new("--hdr-enabled", "Output HDR")
        {
            Description = "Hands the display an HDR signal.",
            Aliases = ["--hdr-enable"]
        };

    private static readonly CommandDefinition Gamescope =
        new("gamescope", "Launch through Gamescope")
        {
            Description = "A nested compositor the game renders into.",
            Groups = [new CommandFlagGroup("HDR", [HdrFlag])]
        };

    [Theory]
    [InlineData("PROTON_ENABLE_WAYLAND")]
    [InlineData("ENABLE_WAY")]
    [InlineData("Run natively")]
    [InlineData("XWayland")]
    public void FindsASettingByItsVariableItsLabelOrItsDescription(string term) =>
        Assert.True(SettingSearch.For(term).Matches(Wayland));

    [Theory]
    [InlineData("wayland")]
    [InlineData("WAYLAND")]
    [InlineData("WaYlAnD")]
    public void IgnoresCase(string term) => Assert.True(SettingSearch.For(term).Matches(Wayland));

    [Fact]
    public void IgnoresTheWhitespaceAroundTheTerm() =>
        Assert.True(SettingSearch.For("  wayland  ").Matches(Wayland));

    [Fact]
    public void FindsNothingItDoesNotName() =>
        Assert.False(SettingSearch.For("mangohud").Matches(Wayland));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MatchesEverythingUntilSomethingIsTyped(string? term)
    {
        var search = SettingSearch.For(term);

        Assert.False(search.IsActive);
        Assert.True(search.Matches(Wayland));
        Assert.True(search.Matches(HdrFlag));
        Assert.True(search.MatchesVariable("ANYTHING_AT_ALL"));
    }

    [Fact]
    public void HasNoActiveTermByDefault() => Assert.False(SettingSearch.None.IsActive);

    [Theory]
    [InlineData("--hdr-enabled")]
    [InlineData("hdr")]
    [InlineData("Output HDR")]
    [InlineData("HDR signal")]
    public void FindsACommandFlag(string term) => Assert.True(SettingSearch.For(term).Matches(HdrFlag));

    [Fact]
    public void FindsACommandFlagUnderAnAlias() =>
        Assert.True(SettingSearch.For("--hdr-enable").Matches(HdrFlag));

    [Fact]
    public void FindsACommandByItsOwnName() =>
        Assert.True(SettingSearch.For("gamescope").Matches(Gamescope));

    [Fact]
    public void FindsACommandThroughAFlagOfIts()
    {
        Assert.True(SettingSearch.For("--hdr-enabled").MatchesAnythingIn(Gamescope));
        Assert.False(SettingSearch.For("--hdr-enabled").Matches(Gamescope));
    }

    [Fact]
    public void FindsNoCommandWhereNeitherItNorItsFlagsAreNamed() =>
        Assert.False(SettingSearch.For("esync").MatchesAnythingIn(Gamescope));

    [Theory]
    [InlineData("PROTON_VKD3D_HEAP", true)]
    [InlineData("vkd3d", true)]
    [InlineData("mangohud", false)]
    public void FindsAVariableItHasNoDefinitionFor(string term, bool expected) =>
        Assert.Equal(expected, SettingSearch.For(term).MatchesVariable("PROTON_VKD3D_HEAP"));
}
