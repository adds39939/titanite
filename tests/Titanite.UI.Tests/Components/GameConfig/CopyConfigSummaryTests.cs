using Titanite.Core.Launch;
using Titanite.UI.Components.GameConfig;

namespace Titanite.UI.Tests.Components.GameConfig;

public class CopyConfigSummaryTests
{
    private static string Describe(string launchOptions) =>
        CopyConfigDialog.Describe(LaunchOptions.Parse(launchOptions));

    [Fact]
    public void CountsTheVariables() =>
        Assert.Equal("2 variables", Describe("PROTON_ENABLE_WAYLAND=1 DXVK_HDR=1 %command%"));

    [Fact]
    public void SaysOneVariableInTheSingular() =>
        Assert.Equal("1 variable", Describe("PROTON_ENABLE_WAYLAND=1 %command%"));

    [Fact]
    public void MentionsTheLaunchChain() =>
        Assert.Equal(
            "1 variable, a launch chain",
            Describe("PROTON_ENABLE_WAYLAND=1 mangohud %command%"));

    [Fact]
    public void MentionsArgumentsPassedToTheGame() =>
        Assert.Equal("1 argument", Describe("%command% -dx11"));

    [Fact]
    public void SaysSoWhenThereIsNothingToTake() => Assert.Equal("Nothing set", Describe(string.Empty));
}
