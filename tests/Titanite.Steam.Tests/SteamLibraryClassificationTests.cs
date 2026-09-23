using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Steam.Library;

namespace Titanite.Steam.Tests;

public sealed class SteamLibraryClassificationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-classify-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task TakesTheKindFromWhatSteamPublished()
    {
        Installed(620, "Portal 2");
        Installed(1826330, "Proton EasyAntiCheat Runtime");

        Published(
            new PublishedApp(620, "Game", "windows"),
            new PublishedApp(1826330, "Tool", "linux"));

        var games = await Scan();

        Assert.False(games["Portal 2"].IsTool);
        Assert.True(games["Proton EasyAntiCheat Runtime"].IsTool);
    }

    [Fact]
    public async Task CallsAToolAToolEvenWithoutAToolManifestBesideIt()
    {
        Installed(1826330, "Proton EasyAntiCheat Runtime");
        Published(new PublishedApp(1826330, "Tool", "linux"));

        Assert.True((await Scan())["Proton EasyAntiCheat Runtime"].IsTool);
    }

    [Fact]
    public async Task KnowsWhichGamesNeedNoProton()
    {
        Installed(620, "Portal 2");
        Installed(570, "Dota 2");

        Published(
            new PublishedApp(620, "Game", "windows"),
            new PublishedApp(570, "Game", "windows,macos,linux"));

        var games = await Scan();

        Assert.False(games["Portal 2"].RunsNatively);
        Assert.True(games["Dota 2"].RunsNatively);
    }

    [Fact]
    public async Task HandsBackToolsAsWellAsGames()
    {
        Installed(620, "Portal 2");
        Installed(1826330, "Proton EasyAntiCheat Runtime");

        Published(
            new PublishedApp(620, "Game", "windows"),
            new PublishedApp(1826330, "Tool", "linux"));

        Assert.Equal(2, (await Scan()).Count);
    }

    [Fact]
    public async Task FallsBackToTheToolManifestWhenSteamPublishedNothing()
    {
        Installed(1628350, "SteamLinuxRuntime_sniper");

        File.WriteAllText(
            Path.Combine(_root, "steamapps", "common", "SteamLinuxRuntime_sniper", "toolmanifest.vdf"),
            "\"manifest\" { }");

        var games = await Scan();

        Assert.True(games["SteamLinuxRuntime_sniper"].IsTool);
        Assert.False(games["SteamLinuxRuntime_sniper"].RunsNatively);
    }

    [Fact]
    public async Task TreatsAGameSteamPublishedNothingForAsNeedingProton()
    {
        Installed(620, "Portal 2");

        var games = await Scan();

        Assert.False(games["Portal 2"].IsTool);
        Assert.False(games["Portal 2"].RunsNatively);
    }

    [Fact]
    public async Task ReadsWhatSteamPublishedAgainOnceItChanges()
    {
        Installed(570, "Dota 2");
        Published(new PublishedApp(570, "Game", "windows"));

        var service = CreateService();

        Assert.False((await Scan(service))["Dota 2"].RunsNatively);

        Published(new PublishedApp(570, "Game", "linux"));
        File.SetLastWriteTimeUtc(Path.Combine(_root, "appcache", "appinfo.vdf"), DateTime.UtcNow.AddMinutes(1));
        service.Invalidate();

        Assert.True((await Scan(service))["Dota 2"].RunsNatively);
    }

    [Fact]
    public async Task ReadsWhatSteamPublishedForAGameInstalledSinceTheLastScan()
    {
        Installed(620, "Portal 2");

        Published(
            new PublishedApp(620, "Game", "windows"),
            new PublishedApp(570, "Game", "windows,macos,linux"));

        var service = CreateService();

        await Scan(service);

        Installed(570, "Dota 2");
        service.Invalidate();

        Assert.True((await Scan(service))["Dota 2"].RunsNatively);
    }

    private SteamLibraryService CreateService() =>
        new(FakeSteamInstall.At(_root), NullLogger<SteamLibraryService>.Instance);

    private Task<IReadOnlyDictionary<string, Titanite.Core.Games.GameEntry>> Scan() => Scan(CreateService());

    private static async Task<IReadOnlyDictionary<string, Titanite.Core.Games.GameEntry>> Scan(
        SteamLibraryService service) =>
        (await service.GetGamesAsync()).ToDictionary(game => game.Name);

    private void Installed(uint appId, string name)
    {
        var steamApps = Path.Combine(_root, "steamapps");

        Directory.CreateDirectory(Path.Combine(steamApps, "common", name));

        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
              "AppState"
              {
                  "appid"      "{{appId}}"
                  "name"       "{{name}}"
                  "installdir" "{{name}}"
                  "StateFlags" "4"
              }
              """);
    }

    private void Published(params PublishedApp[] apps)
    {
        var appCache = Path.Combine(_root, "appcache");

        Directory.CreateDirectory(appCache);
        File.WriteAllBytes(Path.Combine(appCache, "appinfo.vdf"), FakeAppInfo.WithStringTable(apps));
    }
}
