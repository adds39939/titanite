using System.Diagnostics;
using System.Text.RegularExpressions;
using Titanite.Platform.Processes;

namespace Titanite.Platform.Tests.Processes;

public class FlatpakHostProcessesTests
{
    [Fact]
    public void RunsTheCommandOnTheHost()
    {
        var startInfo = FlatpakHostProcesses.OnHost(
            DetachedProcess.BuildStartInfo(detached: true, "steam", ["steam://rungameid/440"]));

        Assert.Equal("flatpak-spawn", startInfo.FileName);
        Assert.Equal(["--host", "setsid", "--fork", "steam", "steam://rungameid/440"], startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void KeepsEachArgumentWhole()
    {
        var startInfo = FlatpakHostProcesses.OnHost(new ProcessStartInfo("pgrep", ["-f", "a b"]));

        Assert.Equal(["--host", "pgrep", "-f", "a b"], startInfo.ArgumentList);
    }

    [Theory]
    [InlineData("SteamLaunch AppId=")]
    [InlineData("a.b*c (d)")]
    [InlineData("-- 1+1")]
    public void MatchesTheTextLiterally(string fragment)
    {
        var pattern = FlatpakHostProcesses.ExtendedRegex.MatchingOthersOnly(fragment);

        Assert.Matches(new Regex(pattern), $"/usr/bin/reaper {fragment}440 -- game");
    }

    [Fact]
    public void GivesPatternCharactersNoMeaning()
    {
        var pattern = FlatpakHostProcesses.ExtendedRegex.MatchingOthersOnly("a.b*");

        Assert.DoesNotMatch(new Regex(pattern), "axbbb");
    }

    [Fact]
    public void DoesNotMatchTheCommandLineThatCarriesIt()
    {
        var pattern = FlatpakHostProcesses.ExtendedRegex.MatchingOthersOnly("SteamLaunch AppId=");

        Assert.Equal("[S]teamLaunch AppId=", pattern);
        Assert.DoesNotMatch(new Regex(pattern), $"flatpak-spawn --host pgrep -f -- {pattern}");
    }
}
