using Titanite.Steam.Library;

namespace Titanite.Steam.Tests;

public class SteamPathsTests
{
    private const string LibraryPath = "/games/SteamLibrary";

    private const uint AppId = 2357570;

    [Fact]
    public void PutsThePrefixInsideTheCompatDataDirectory() =>
        Assert.Equal(
            Path.Combine(SteamPaths.CompatDataDirectory(LibraryPath, AppId), "pfx"),
            SteamPaths.PrefixDirectory(LibraryPath, AppId));

    [Fact]
    public void KeepsCompatDataBesideTheLibrarysSteamAppsFolder() =>
        Assert.Equal(
            Path.Combine(LibraryPath, "steamapps", "compatdata", AppId.ToString()),
            SteamPaths.CompatDataDirectory(LibraryPath, AppId));

    [Fact]
    public void NamesTheCompatDataDirectoryAfterTheApp() =>
        Assert.EndsWith(
            AppId.ToString(),
            SteamPaths.CompatDataDirectory(LibraryPath, AppId),
            StringComparison.Ordinal);
}
