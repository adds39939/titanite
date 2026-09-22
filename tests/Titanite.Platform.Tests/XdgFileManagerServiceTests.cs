using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Desktop;
using Titanite.Platform.Desktop;

namespace Titanite.Platform.Tests;

public class XdgFileManagerServiceTests
{
    [Fact]
    public void OpensTheFolderInASessionOfItsOwn()
    {
        var startInfo = XdgFileManagerService.BuildStartInfo(detached: true, "/games/Half-Life");

        Assert.Equal("setsid", startInfo.FileName);
        Assert.Equal(["--fork", "xdg-open", "/games/Half-Life"], startInfo.ArgumentList);
    }

    [Fact]
    public void FallsBackToOpeningTheFolderDirectly()
    {
        var startInfo = XdgFileManagerService.BuildStartInfo(detached: false, "/games/Half-Life");

        Assert.Equal("xdg-open", startInfo.FileName);
        Assert.Equal(["/games/Half-Life"], startInfo.ArgumentList);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PassesTheWholePathAsOneArgument(bool detached)
    {
        const string path = "/games/Half-Life 2/\"episode\" one";

        var startInfo = XdgFileManagerService.BuildStartInfo(detached, path);

        Assert.Contains(path, startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportsAPathThatNamesNothingWithoutAskingTheDesktop(string path) =>
        Assert.Equal(DirectoryOpenStatus.NotFound, Open().OpenDirectory(path));

    [Fact]
    public void ReportsAFolderThatIsNotThere()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"titanite-{Guid.NewGuid():N}", "pfx");

        Assert.Equal(DirectoryOpenStatus.NotFound, Open().OpenDirectory(missing));
    }

    [Fact]
    public void ReportsAPathThatNamesAFileRatherThanAFolder()
    {
        var file = Path.Combine(Path.GetTempPath(), $"titanite-{Guid.NewGuid():N}.tmp");

        File.WriteAllText(file, string.Empty);

        try
        {
            Assert.Equal(DirectoryOpenStatus.NotFound, Open().OpenDirectory(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static XdgFileManagerService Open() =>
        new(NullLogger<XdgFileManagerService>.Instance);
}
