using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class LaunchOptionsTests
{
    private const string OverwatchOptions =
        "PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 PROTON_ENABLE_NGX_UPDATER=1 " +
        "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L " +
        "DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS=DLSSIndicator=1024 " +
        "MANGOHUD_CONFIG=fps_limit=224,fps_limit_method=late " +
        "taskset -c 0-7,16-23 %command%";

    [Theory]
    [InlineData("")]
    [InlineData("%command%")]
    [InlineData("PROTON_ENABLE_NVAPI=1 %command%")]
    [InlineData("mangohud %command%")]
    [InlineData("%command% -windowed -novid")]
    [InlineData("gamemoderun mangohud %command%")]
    [InlineData("WINEDLLOVERRIDES=\"dxgi=n,b\" %command%")]
    [InlineData("PROTON_LOG=1")]
    [InlineData("-novid -console")]
    [InlineData("DXVK_CONFIG_FILE=\"/home/adam/my games/dxvk.conf\" %command%")]
    [InlineData(@"DXVK_CONFIG_FILE=/home/adam/my\ games/dxvk.conf %command%")]
    [InlineData("WINEDLLOVERRIDES='dxgi=n,b' %command%")]
    [InlineData(OverwatchOptions)]
    public void FormatRestoresTheOriginalString(string original) =>
        Assert.Equal(original, LaunchOptions.Parse(original).Format());

    [Fact]
    public void ParsesEnvironmentAssignmentsInOrder()
    {
        var options = LaunchOptions.Parse(OverwatchOptions);

        Assert.Equal(
            [
                "PROTON_ENABLE_WAYLAND",
                "PROTON_ENABLE_HDR",
                "DXVK_HDR",
                "PROTON_ENABLE_NGX_UPDATER",
                "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION",
                "DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS",
                "MANGOHUD_CONFIG"
            ],
            options.Environment.Select(variable => variable.Name));
    }

    [Fact]
    public void KeepsCompoundValuesIntact()
    {
        var options = LaunchOptions.Parse(OverwatchOptions);

        Assert.Equal("DLSSIndicator=1024", options.FindEnvironment("DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS")?.Value);
        Assert.Equal("fps_limit=224,fps_limit_method=late", options.FindEnvironment("MANGOHUD_CONFIG")?.Value);
        Assert.Equal("RENDER_PRESET_L",
            options.FindEnvironment("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION")?.Value);
    }

    [Fact]
    public void ParsesWrapperChainWithItsArguments()
    {
        var options = LaunchOptions.Parse(OverwatchOptions);

        Assert.Equal(
            ["taskset", "-c", "0-7,16-23"],
            options.Wrapper);
        Assert.True(options.HasCommandPlaceholder);
        Assert.Empty(options.Arguments);
    }

    [Fact]
    public void TreatsAssignmentsAfterAWrapperAsArgumentsNotEnvironment()
    {
        var options = LaunchOptions.Parse("PROTON_ENABLE_HDR=1 mysetting FOO=bar %command%");

        Assert.Equal(["PROTON_ENABLE_HDR"], options.Environment.Select(variable => variable.Name));
        Assert.Equal(["mysetting", "FOO=bar"], options.Wrapper);
    }

    [Fact]
    public void SeparatesArgumentsFromWrappersAroundThePlaceholder()
    {
        var options = LaunchOptions.Parse("mangohud %command% -windowed");

        Assert.Equal(["mangohud"], options.Wrapper);
        Assert.Equal(["-windowed"], options.Arguments);
    }

    [Fact]
    public void RecordsWhenThePlaceholderIsMissing()
    {
        var options = LaunchOptions.Parse("PROTON_LOG=1 -novid");

        Assert.False(options.HasCommandPlaceholder);
        Assert.Empty(options.Wrapper);
        Assert.Equal(["-novid"], options.Arguments);
    }

    [Fact]
    public void ResolvesQuotedValues()
    {
        var options = LaunchOptions.Parse("DXVK_CONFIG_FILE=\"/home/adam/my games/dxvk.conf\" %command%");

        Assert.Equal("/home/adam/my games/dxvk.conf", options.FindEnvironment("DXVK_CONFIG_FILE")?.Value);
    }

    [Fact]
    public void ResolvesEscapedSpacesWithoutRewritingThem()
    {
        var options = LaunchOptions.Parse(@"DXVK_CONFIG_FILE=/home/adam/my\ games/dxvk.conf %command%");

        Assert.Equal("/home/adam/my games/dxvk.conf", options.FindEnvironment("DXVK_CONFIG_FILE")?.Value);
    }

    [Fact]
    public void ReQuotesAnAssignmentOnceItsValueChanges()
    {
        var original = LaunchOptions.Parse("WINEDLLOVERRIDES=\"dxgi=n,b\" %command%");

        var edited = original with
        {
            Environment = [original.Environment[0] with { Value = "d3d11=n,b" }]
        };

        Assert.Equal("WINEDLLOVERRIDES=d3d11=n,b %command%", edited.Format());
    }

    [Fact]
    public void QuotesAnAddedValueThatNeedsIt()
    {
        var options = new LaunchOptions
        {
            Environment = [new EnvironmentVariable("DXVK_CONFIG_FILE", "/home/adam/my games/dxvk.conf")],
            HasCommandPlaceholder = true
        };

        Assert.Equal("DXVK_CONFIG_FILE=\"/home/adam/my games/dxvk.conf\" %command%", options.Format());
    }

    [Fact]
    public void PreservesAnEmptyAssignment()
    {
        var options = LaunchOptions.Parse("WINEDLLOVERRIDES= %command%");

        Assert.Equal(string.Empty, options.FindEnvironment("WINEDLLOVERRIDES")?.Value);
        Assert.Equal("WINEDLLOVERRIDES= %command%", options.Format());
    }

    [Fact]
    public void CollapsesIrregularWhitespace()
    {
        var options = LaunchOptions.Parse("  PROTON_ENABLE_HDR=1   mangohud    %command%  ");

        Assert.Equal("PROTON_ENABLE_HDR=1 mangohud %command%", options.Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsBlankInputAsEmpty(string? original)
    {
        var options = LaunchOptions.Parse(original);

        Assert.True(options.IsEmpty);
        Assert.Equal(string.Empty, options.Format());
    }

    [Fact]
    public void EditingOneSettingLeavesUnknownOnesAlone()
    {
        var options = LaunchOptions.Parse(OverwatchOptions);

        var edited = options with
        {
            Environment = options.Environment
                .Select(variable => variable.Name == "PROTON_ENABLE_HDR"
                    ? variable with { Value = "0" }
                    : variable)
                .ToList()
        };

        Assert.Equal(OverwatchOptions.Replace("PROTON_ENABLE_HDR=1", "PROTON_ENABLE_HDR=0"), edited.Format());
    }
}
