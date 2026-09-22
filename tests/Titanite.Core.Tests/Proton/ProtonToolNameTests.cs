using Titanite.Core.Proton;

namespace Titanite.Core.Tests.Proton;

public class ProtonToolNameTests
{
    [Theory]
    [InlineData("Proton Experimental", "proton_experimental")]
    [InlineData("Proton Hotfix", "proton_hotfix")]
    public void DerivesNamedBuilds(string appName, string expected) =>
        Assert.Equal(expected, ProtonToolName.Derive(appName));

    [Theory]
    [InlineData("Proton 3.7", "proton_37")]
    [InlineData("Proton 3.16", "proton_316")]
    [InlineData("Proton 4.2", "proton_42")]
    [InlineData("Proton 4.11", "proton_411")]
    [InlineData("Proton 5.13", "proton_513")]
    [InlineData("Proton 6.3", "proton_63")]
    public void DerivesVersionedBuilds(string appName, string expected) =>
        Assert.Equal(expected, ProtonToolName.Derive(appName));

    [Theory]
    [InlineData("Proton 5.0", "proton_5")]
    [InlineData("Proton 7.0", "proton_7")]
    [InlineData("Proton 8.0", "proton_8")]
    [InlineData("Proton 9.0", "proton_9")]
    [InlineData("Proton 10.0", "proton_10")]
    [InlineData("Proton 11.0", "proton_11")]
    public void DropsATrailingZeroMinorVersion(string appName, string expected) =>
        Assert.Equal(expected, ProtonToolName.Derive(appName));

    [Fact]
    public void IgnoresSurroundingWhitespace() =>
        Assert.Equal("proton_9", ProtonToolName.Derive("  Proton 9.0  "));

    [Fact]
    public void CollapsesPunctuationInUnfamiliarNames() =>
        Assert.Equal("proton_easyanticheat_runtime", ProtonToolName.Derive("Proton EasyAntiCheat Runtime"));
}
