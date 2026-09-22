using Titanite.Core.Launch;
using Titanite.Core.Proton;

namespace Titanite.Core.Tests.Launch;

public class SettingDefinitionTests
{
    private static readonly SettingCategory Dlss = new("dlss", "DLSS", 1);

    private static SettingDefinition Definition(params string[] protonBuilds) =>
        new("PROTON_DLSS_UPGRADE", Dlss, "Upgrade DLSS libraries") { ProtonBuilds = protonBuilds };

    private static ProtonBuild Build(string name, string? version = null) => new()
    {
        Name = name,
        DisplayName = name,
        InstallPath = $"/steam/{name}",
        Kind = ProtonBuildKind.Custom,
        Version = version
    };

    [Fact]
    public void AppliesEverywhereWhenNoBuildsAreNamed() =>
        Assert.True(Definition().AppliesTo(Build("proton_experimental")));

    [Fact]
    public void AppliesWhenThereIsNoBuildToJudge() =>
        Assert.True(Definition("^GE-Proton").AppliesTo(null));

    [Fact]
    public void MatchesTheBuildsItNames()
    {
        var definition = Definition("^GE-Proton");

        Assert.True(definition.AppliesTo(Build("GE-Proton11-3")));
        Assert.False(definition.AppliesTo(Build("proton_experimental")));
    }

    [Fact]
    public void MatchesOnTheVersionAsWellAsTheName() =>
        Assert.True(Definition("^GE-Proton").AppliesTo(Build("renamed-by-hand", "GE-Proton11-3")));

    [Fact]
    public void MatchesAnyOfSeveralPatterns()
    {
        var definition = Definition("^GE-Proton", "cachyos");

        Assert.True(definition.AppliesTo(Build("proton-cachyos-10")));
        Assert.True(definition.AppliesTo(Build("GE-Proton11-3")));
        Assert.False(definition.AppliesTo(Build("proton_hotfix")));
    }

    [Fact]
    public void IgnoresCasing() =>
        Assert.True(Definition("^ge-proton").AppliesTo(Build("GE-Proton11-3")));

    [Fact]
    public void TreatsAMalformedPatternAsMatchingNothing()
    {
        var definition = Definition("^GE-Proton", "[unclosed");

        Assert.True(definition.AppliesTo(Build("GE-Proton11-3")));
        Assert.False(definition.AppliesTo(Build("proton_experimental")));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("DLSSIndicator=1024", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ReadsWhetherAStoredValueMeansOn(string? value, bool expected) =>
        Assert.Equal(expected, Definition().IsOn(value));

    [Fact]
    public void KeepsAValueTheDefinitionsDoNotList()
    {
        var options = LaunchOptions.Parse(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_ZZ %command%");

        Assert.Equal(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_ZZ %command%",
            options.Format());
    }
}
