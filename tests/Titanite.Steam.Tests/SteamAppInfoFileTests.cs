using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class SteamAppInfoFileTests
{
    [Fact]
    public void ReadsWhatSteamPublishedAboutEachApp()
    {
        var published = SteamAppInfoFile.Parse(FakeAppInfo.WithStringTable(
            new PublishedApp(620, "Game", "windows,macos"),
            new PublishedApp(1826330, "Tool", "linux")));

        Assert.Equal("Game", published[620].Type);
        Assert.Equal("windows,macos", published[620].OperatingSystems);
        Assert.Equal("Tool", published[1826330].Type);
        Assert.Equal("linux", published[1826330].OperatingSystems);
    }

    [Fact]
    public void ReadsTheOlderShapeThatSpellsEveryKeyOut()
    {
        var published = SteamAppInfoFile.Parse(FakeAppInfo.WithInlineKeys(
            new PublishedApp(440, "Game", "windows,macos,linux")));

        Assert.Equal("Game", published[440].Type);
        Assert.True(published[440].RunsOnLinux);
    }

    [Theory]
    [InlineData("Tool", true)]
    [InlineData("tool", true)]
    [InlineData("Game", false)]
    [InlineData("Application", false)]
    [InlineData(null, false)]
    public void KnowsWhichKindsAreTools(string? type, bool expected) =>
        Assert.Equal(expected, new SteamAppMetadata(type, null).IsTool);

    [Theory]
    [InlineData("linux", true)]
    [InlineData("windows,macos,linux", true)]
    [InlineData("windows, linux", true)]
    [InlineData("LINUX", true)]
    [InlineData("windows", false)]
    [InlineData("windows,macos", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void KnowsWhichAppsRunWithoutProton(string? operatingSystems, bool expected) =>
        Assert.Equal(expected, new SteamAppMetadata(null, operatingSystems).RunsOnLinux);

    [Fact]
    public void SaysNothingAboutAnAppSteamPublishedNothingFor() =>
        Assert.False(SteamAppInfoFile
            .Parse(FakeAppInfo.WithStringTable(new PublishedApp(620, "Game", "windows")))
            .ContainsKey(440));

    [Fact]
    public void TreatsAFileItDoesNotRecogniseAsEmpty() =>
        Assert.Empty(SteamAppInfoFile.Parse("not an appinfo file"u8));

    [Fact]
    public void TreatsATruncatedFileAsEmpty() =>
        Assert.Empty(SteamAppInfoFile.Parse([0x29, 0x44]));

    [Fact]
    public void StopsAtAnAppLengthThatRunsOffTheEnd()
    {
        var bytes = FakeAppInfo.WithStringTable(new PublishedApp(620, "Game", "windows"));

        bytes[20] = 0xFF;
        bytes[21] = 0xFF;

        Assert.Empty(SteamAppInfoFile.Parse(bytes));
    }

    [Fact]
    public void ReadsNothingFromAFileThatIsNotThere() =>
        Assert.Empty(SteamAppInfoFile.Read(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.vdf")));
}
