using Titanite.Steam.Client;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public class SteamClientLaunchTests
{
    [Fact]
    public void StartsSteamInASessionOfItsOwn()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: true, []);

        Assert.Equal("setsid", startInfo.FileName);
        Assert.Equal(["--fork", "steam"], startInfo.ArgumentList);
    }

    [Fact]
    public void LaunchesAGameThroughSteamsOwnAddress()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: true, [SteamClient.GameUrl(440)]);

        Assert.Equal(["--fork", "steam", "steam://rungameid/440"], startInfo.ArgumentList);
    }

    [Fact]
    public void PassesArgumentsThroughToSteam()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: true, ["-shutdown"]);

        Assert.Equal(["--fork", "steam", "-shutdown"], startInfo.ArgumentList);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeverCapturesSteamsOutput(bool detached)
    {
        var startInfo = SteamClient.BuildStartInfo(detached, ["-shutdown"]);

        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void FallsBackToLaunchingSteamDirectly()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: false, ["-shutdown"]);

        Assert.Equal("steam", startInfo.FileName);
        Assert.Equal(["-shutdown"], startInfo.ArgumentList);
    }
}
