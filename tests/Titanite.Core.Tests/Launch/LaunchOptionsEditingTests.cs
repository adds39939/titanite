using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class LaunchOptionsEditingTests
{
    private const string Options = "PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 mangohud %command%";

    [Fact]
    public void SettingAnExistingVariableLeavesItWhereItWas()
    {
        var edited = LaunchOptions.Parse(Options).SetEnvironment("PROTON_ENABLE_WAYLAND", "0");

        Assert.Equal("PROTON_ENABLE_WAYLAND=0 PROTON_ENABLE_HDR=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void SettingANewVariableAppendsIt()
    {
        var edited = LaunchOptions.Parse(Options).SetEnvironment("DXVK_HDR", "1");

        Assert.Equal("PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void RemovingAVariableLeavesTheOthersAlone()
    {
        var edited = LaunchOptions.Parse(Options).RemoveEnvironment("PROTON_ENABLE_HDR");

        Assert.Equal("PROTON_ENABLE_WAYLAND=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void SwitchingAVariableOffAndOnAgainLeavesItWhereItWas()
    {
        var edited = LaunchOptions
            .Parse(Options)
            .RemoveEnvironment("PROTON_ENABLE_WAYLAND")
            .SetEnvironment("PROTON_ENABLE_WAYLAND", "1");

        Assert.Equal(Options, edited.Format());
    }

    [Fact]
    public void RestoresAVariableFromTheMiddleOfTheRun()
    {
        const string original = "A=1 B=1 C=1 mangohud %command%";

        var edited = LaunchOptions.Parse(original).RemoveEnvironment("B").SetEnvironment("B", "1");

        Assert.Equal(original, edited.Format());
    }

    [Fact]
    public void RestoresSeveralVariablesToTheirOwnPlaces()
    {
        const string original = "A=1 B=1 C=1 D=1 %command%";

        var edited = LaunchOptions
            .Parse(original)
            .RemoveEnvironment("B")
            .RemoveEnvironment("C")
            .SetEnvironment("C", "1")
            .SetEnvironment("B", "1");

        Assert.Equal(original, edited.Format());
    }

    [Fact]
    public void StillAppendsSomethingThatWasNeverThere()
    {
        var edited = LaunchOptions
            .Parse(Options)
            .RemoveEnvironment("PROTON_ENABLE_HDR")
            .SetEnvironment("DXVK_HDR", "1");

        Assert.Equal("PROTON_ENABLE_WAYLAND=1 DXVK_HDR=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void ForgetsWhereAVariableSatOnceTheStringHasBeenSaved()
    {
        var saved = LaunchOptions.Parse(Options).RemoveEnvironment("PROTON_ENABLE_WAYLAND").Format();

        var edited = LaunchOptions.Parse(saved).SetEnvironment("PROTON_ENABLE_WAYLAND", "1");

        Assert.Equal("PROTON_ENABLE_HDR=1 PROTON_ENABLE_WAYLAND=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void RemovingSomethingThatIsNotThereChangesNothing() =>
        Assert.Equal(Options, LaunchOptions.Parse(Options).RemoveEnvironment("DXVK_HDR").Format());

    [Fact]
    public void EditingNeverDisturbsTheLaunchChain()
    {
        var edited = LaunchOptions
            .Parse("A=1 /home/adam/bin/ow-dlss mangohud taskset -c 0-7 %command%")
            .SetEnvironment("B", "2")
            .RemoveEnvironment("A");

        Assert.Equal("B=2 /home/adam/bin/ow-dlss mangohud taskset -c 0-7 %command%", edited.Format());
    }
}

public class LaunchOptionsValidatorTests
{
    [Fact]
    public void WarnsWhenHdrIsEnabledWithoutWayland()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 %command%"));

        Assert.Contains(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void StaysQuietWhenHdrHasWayland()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void StaysQuietWhenHdrHasWaylandUnderTheOlderSpelling()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("PROTON_USE_WAYLAND=1 PROTON_USE_HDR=1 %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnsWhenHdrIsEnabledUnderTheOlderSpellingWithoutWayland()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse("PROTON_USE_HDR=1 %command%"));

        Assert.Contains(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void SaysNothingAboutADlssOverrideWithoutNvapi()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L %command%"));

        Assert.Empty(warnings);
    }

    [Fact]
    public void WarnsWhenMangoHudIsConfiguredButNotLaunched()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("MANGOHUD_CONFIG=fps_limit=60 %command%"));

        Assert.Contains(warnings, warning => warning.Contains("mangohud", StringComparison.Ordinal));
    }

    [Fact]
    public void RecognisesMangoHudBehindAnAbsolutePath()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("MANGOHUD_CONFIG=fps_limit=60 /usr/bin/mangohud %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("mangohud", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsGamescopeDrawingTheOverlayInsteadOfMangoHud()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("MANGOHUD_CONFIG=fps_limit=60 gamescope --mangoapp -- %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("mangohud", StringComparison.Ordinal));
    }

    [Fact]
    public void WarnsWhenGamescopeAndMangoHudAreStacked()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("gamescope -f -- mangohud %command%"));

        Assert.Contains(warnings, warning => warning.Contains("--mangoapp", StringComparison.Ordinal));
    }

    [Fact]
    public void SaysNothingAboutGamescopeHdrWithoutTheVariableItSetsItself()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("gamescope --hdr-enabled -- %command%"));

        Assert.Empty(warnings);
    }

    [Fact]
    public void WarnsWhenInverseToneMappingHasNoHdrToMapInto()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "gamescope --hdr-itm-enabled --hdr-itm-target-nits 800 -- %command%"));

        Assert.Contains(warnings, warning => warning.Contains("--hdr-enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void RecognisesTheAbbreviatedSpellingOfInverseToneMapping()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("gamescope --hdr-itm-enable -- %command%"));

        Assert.Contains(warnings, warning => warning.Contains("--hdr-enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void HasNothingToSayAboutARealGamescopeConfiguration()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "DXVK_HDR=1 PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 " +
            "gamescope -W 3840 -H 2160 -r 240 -f --hdr-enabled --hdr-itm-enabled " +
            "--hdr-itm-sdr-nits 203 --hdr-itm-target-nits 800 --adaptive-sync -- %command%"));

        Assert.Empty(warnings);
    }

    [Fact]
    public void HasNothingToSayAboutARealConfiguration()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 " +
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L " +
            "MANGOHUD_CONFIG=fps_limit=224 mangohud %command%"));

        Assert.Empty(warnings);
    }
}
