using Titanite.Core.Proton;

namespace Titanite.Core.Tests.Proton;

public class ProtonCapabilitiesTests
{
    private static ProtonCapabilities Reading(params string[] variables) => new()
    {
        Variables = variables.ToHashSet(StringComparer.Ordinal),
        IsKnown = true
    };

    [Fact]
    public void AnswersForAVariableTheBuildReads() =>
        Assert.True(Reading("PROTON_DLSS_UPGRADE").Reads("PROTON_DLSS_UPGRADE"));

    [Fact]
    public void AnswersForAVariableTheBuildDoesNotRead()
    {
        var capabilities = Reading("PROTON_LOG");

        Assert.False(capabilities.Reads("PROTON_ENABLE_NGX_UPDATER"));
        Assert.True(capabilities.Ignores("PROTON_ENABLE_NGX_UPDATER"));
    }

    [Theory]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_HDR")]
    [InlineData("VKD3D_CONFIG")]
    [InlineData("MANGOHUD_CONFIG")]
    public void HasNoOpinionOnVariablesItCannotSee(string variable)
    {
        var capabilities = Reading("PROTON_LOG");

        Assert.Null(capabilities.Reads(variable));
        Assert.False(capabilities.Ignores(variable));
    }

    [Fact]
    public void JudgesNothingWhenTheBuildCouldNotBeRead()
    {
        Assert.Null(ProtonCapabilities.Unknown.Reads("PROTON_LOG"));
        Assert.False(ProtonCapabilities.Unknown.Ignores("PROTON_ENABLE_NGX_UPDATER"));
    }

    [Fact]
    public void MatchesNamesExactly() =>
        Assert.Null(Reading("PROTON_LOG").Reads("proton_log"));
}
