using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Launch;

public class LaunchOptionsDiffTests
{
    private static IReadOnlyList<LaunchDiffToken> Compare(string saved, string pending) =>
        LaunchOptionsDiff.Compare(LaunchOptions.Parse(saved), LaunchOptions.Parse(pending));

    private static IEnumerable<string> Of(IReadOnlyList<LaunchDiffToken> tokens, LaunchDiffKind kind) =>
        tokens.Where(token => token.Kind == kind).Select(token => token.Text);

    [Fact]
    public void MarksNothingWhenNothingChanged()
    {
        var diff = Compare("PROTON_ENABLE_HDR=1 %command%", "PROTON_ENABLE_HDR=1 %command%");

        Assert.All(diff, token => Assert.Equal(LaunchDiffKind.Unchanged, token.Kind));
    }

    [Fact]
    public void MarksANewSettingAsAdded()
    {
        var diff = Compare("PROTON_ENABLE_HDR=1 %command%", "PROTON_ENABLE_HDR=1 DXVK_HDR=1 %command%");

        Assert.Equal(["DXVK_HDR=1"], Of(diff, LaunchDiffKind.Added));
        Assert.Empty(Of(diff, LaunchDiffKind.Removed));
    }

    [Fact]
    public void MarksARemovedSettingAsRemoved()
    {
        var diff = Compare("PROTON_ENABLE_HDR=1 DXVK_HDR=1 %command%", "PROTON_ENABLE_HDR=1 %command%");

        Assert.Equal(["DXVK_HDR=1"], Of(diff, LaunchDiffKind.Removed));
        Assert.Empty(Of(diff, LaunchDiffKind.Added));
    }

    [Fact]
    public void ShowsAChangedValueAsBothSides()
    {
        var diff = Compare(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L %command%",
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_K %command%");

        Assert.Single(Of(diff, LaunchDiffKind.Removed));
        Assert.Single(Of(diff, LaunchDiffKind.Added));
    }

    [Fact]
    public void MarksEverythingAddedWhenNothingWasSet()
    {
        var diff = Compare(string.Empty, "PROTON_ENABLE_HDR=1 mangohud %command%");

        Assert.Equal(["PROTON_ENABLE_HDR=1", "mangohud", "%command%"], Of(diff, LaunchDiffKind.Added));
    }

    [Fact]
    public void KeepsAQuotedValueWhole()
    {
        var diff = Compare(string.Empty, "DXVK_CONFIG_FILE=\"/home/adam/my games/dxvk.conf\" %command%");

        Assert.Contains("DXVK_CONFIG_FILE=\"/home/adam/my games/dxvk.conf\"", Of(diff, LaunchDiffKind.Added));
    }

    [Fact]
    public void ReadsTheRealChangeMadeToRematch()
    {
        var diff = Compare(
            "PROTON_ENABLE_NVAPI=1 %command%",
            "PROTON_ENABLE_NVAPI=1 /home/adam/.local/share/titanite/bin/dlss-2138720.sh %command%");

        Assert.Equal(["/home/adam/.local/share/titanite/bin/dlss-2138720.sh"], Of(diff, LaunchDiffKind.Added));
        Assert.Empty(Of(diff, LaunchDiffKind.Removed));
    }

    [Fact]
    public void TheKeptTokensAreExactlyWhatWillBeWritten()
    {
        const string pending = "A=1 gamemoderun taskset -c 0-7 %command% -novid";

        var diff = Compare("A=1 mangohud %command%", pending);

        Assert.Equal(
            pending,
            string.Join(' ', diff.Where(t => t.Kind != LaunchDiffKind.Removed).Select(t => t.Text)));
    }
}
