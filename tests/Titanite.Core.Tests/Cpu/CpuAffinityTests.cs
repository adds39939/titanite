using Titanite.Core.Cpu;
using Titanite.Core.Launch;

namespace Titanite.Core.Tests.Cpu;

public class CpuAffinityMaskTests
{
    [Theory]
    [InlineData("0", new[] { 0 })]
    [InlineData("0-3", new[] { 0, 1, 2, 3 })]
    [InlineData("0-7,16-23", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23 })]
    [InlineData("1,3,5", new[] { 1, 3, 5 })]
    [InlineData("0-1,4", new[] { 0, 1, 4 })]
    public void FormatsRunsAsRanges(string expected, int[] threads) =>
        Assert.Equal(expected, CpuAffinityMask.Format(threads));

    [Fact]
    public void FormatsNothingForAnEmptySet() => Assert.Equal(string.Empty, CpuAffinityMask.Format([]));

    [Theory]
    [InlineData("0-7,16-23")]
    [InlineData("1,3,5")]
    [InlineData("0")]
    [InlineData("8-15,24-31")]
    public void ParsingAndFormattingRoundTrip(string mask) =>
        Assert.Equal(mask, CpuAffinityMask.Format(CpuAffinityMask.Parse(mask)));

    [Fact]
    public void SortsAndDeduplicatesWhatItIsGiven() =>
        Assert.Equal("0-2", CpuAffinityMask.Format([2, 0, 1, 2]));

    [Theory]
    [InlineData(" 0 - 3 ")]
    [InlineData("0-3,")]
    [InlineData("0-3,,")]
    public void ToleratesUntidyInput(string mask) =>
        Assert.Equal([0, 1, 2, 3], CpuAffinityMask.Parse(mask));

    [Fact]
    public void SkipsSectionsItCannotRead() =>
        Assert.Equal([0, 1, 5], CpuAffinityMask.Parse("0-1,banana,5,9-3"));
}

public class LaunchOptionsWrapperTests
{
    private const string Overwatch =
        "PROTON_ENABLE_HDR=1 /home/adam/bin/ow-dlss mangohud taskset -c 0-7,16-23 %command%";

    [Fact]
    public void ReadsTheAffinityMask() =>
        Assert.Equal("0-7,16-23", LaunchOptions.Parse(Overwatch).CpuAffinity);

    [Fact]
    public void ReportsNoAffinityWhenTheGameIsNotPinned() =>
        Assert.Null(LaunchOptions.Parse("mangohud %command%").CpuAffinity);

    [Fact]
    public void ChangingTheMaskLeavesTheRestOfTheChainAlone()
    {
        var edited = LaunchOptions.Parse(Overwatch).WithCpuAffinity("8-15,24-31");

        Assert.Equal(
            "PROTON_ENABLE_HDR=1 /home/adam/bin/ow-dlss mangohud taskset -c 8-15,24-31 %command%",
            edited.Format());
    }

    [Fact]
    public void ClearingTheMaskRemovesTasksetEntirely()
    {
        var edited = LaunchOptions.Parse(Overwatch).WithCpuAffinity(null);

        Assert.Equal("PROTON_ENABLE_HDR=1 /home/adam/bin/ow-dlss mangohud %command%", edited.Format());
    }

    [Fact]
    public void PinningAnUnpinnedGamePutsTasksetLast()
    {
        var edited = LaunchOptions.Parse("mangohud %command%").WithCpuAffinity("0-7");

        Assert.Equal("mangohud taskset -c 0-7 %command%", edited.Format());
    }

    [Fact]
    public void UnderstandsTheLongFormFlag() =>
        Assert.Equal("0-3", LaunchOptions.Parse("taskset --cpu-list 0-3 %command%").CpuAffinity);

    [Fact]
    public void RecognisesTasksetBehindAnAbsolutePath() =>
        Assert.Equal("0-3", LaunchOptions.Parse("/usr/bin/taskset -c 0-3 %command%").CpuAffinity);

    [Fact]
    public void AddsABareWrapperAtTheFront() =>
        Assert.Equal(
            "mangohud taskset -c 0-7 %command%",
            LaunchOptions.Parse("taskset -c 0-7 %command%").WithWrapperCommand("mangohud", true).Format());

    [Fact]
    public void RemovesOnlyTheCommandItWasAskedTo()
    {
        var edited = LaunchOptions.Parse(Overwatch).WithWrapperCommand("mangohud", false);

        Assert.Equal(
            "PROTON_ENABLE_HDR=1 /home/adam/bin/ow-dlss taskset -c 0-7,16-23 %command%",
            edited.Format());
    }

    [Fact]
    public void AddingSomethingAlreadyPresentChangesNothing() =>
        Assert.Equal(Overwatch, LaunchOptions.Parse(Overwatch).WithWrapperCommand("mangohud", true).Format());

    [Fact]
    public void FindsAnAbsoluteScriptItAddedItself()
    {
        var script = "/home/adam/.local/share/titanite/bin/dlss-2357570.sh";
        var edited = LaunchOptions.Parse("%command%").WithWrapperCommand(script, true);

        Assert.True(edited.HasWrapperCommand(script));
        Assert.Equal($"{script} %command%", edited.Format());
        Assert.Equal("%command%", edited.WithWrapperCommand(script, false).Format());
    }

    [Fact]
    public void NeverDisturbsACustomScript()
    {
        var edited = LaunchOptions
            .Parse(Overwatch)
            .WithCpuAffinity("0-3")
            .WithWrapperCommand("gamemoderun", true);

        Assert.Contains("/home/adam/bin/ow-dlss", edited.Wrapper);
        Assert.Equal(
            "PROTON_ENABLE_HDR=1 gamemoderun /home/adam/bin/ow-dlss mangohud taskset -c 0-3 %command%",
            edited.Format());
    }
}
