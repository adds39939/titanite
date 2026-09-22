using Titanite.Platform.Processes;

namespace Titanite.Platform.Tests.Processes;

public class DetachedProcessTests
{
    [Fact]
    public void StartsTheCommandInASessionOfItsOwn()
    {
        var startInfo = DetachedProcess.BuildStartInfo(detached: true, "steam", ["-shutdown"]);

        Assert.Equal("setsid", startInfo.FileName);
        Assert.Equal(["--fork", "steam", "-shutdown"], startInfo.ArgumentList);
    }

    [Fact]
    public void FallsBackToStartingTheCommandDirectly()
    {
        var startInfo = DetachedProcess.BuildStartInfo(detached: false, "steam", ["-shutdown"]);

        Assert.Equal("steam", startInfo.FileName);
        Assert.Equal(["-shutdown"], startInfo.ArgumentList);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeverCapturesTheCommandsOutput(bool detached)
    {
        var startInfo = DetachedProcess.BuildStartInfo(detached, "steam", []);

        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }
}
