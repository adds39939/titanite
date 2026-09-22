using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Steam.Library;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class SteamLibraryCachingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-library-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamLibraryService CreateService() =>
        new(FakeSteamInstall.At(_root), NullLogger<SteamLibraryService>.Instance);

    private void WriteApp(uint appId, string name)
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

    private void DeleteApp(uint appId) =>
        File.Delete(Path.Combine(_root, "steamapps", $"appmanifest_{appId}.acf"));

    [Fact]
    public async Task ReadsTheLibraryOnTheFirstAsk()
    {
        WriteApp(440, "Team Fortress 2");

        var apps = await CreateService().GetInstalledAppsAsync();

        Assert.Equal(["Team Fortress 2"], apps.Select(app => app.Name));
    }

    [Fact]
    public async Task AnswersAgainWithoutReadingTheDiskAgain()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        var first = await service.GetInstalledAppsAsync();

        DeleteApp(440);

        var second = await service.GetInstalledAppsAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ReadsTheDiskAgainOnceTheAnswerIsDiscarded()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        await service.GetInstalledAppsAsync();

        WriteApp(620, "Portal 2");
        service.Invalidate();

        var apps = await service.GetInstalledAppsAsync();

        Assert.Equal(["Portal 2", "Team Fortress 2"], apps.Select(app => app.Name));
    }

    [Fact]
    public async Task ScansOnceWhenAskedFromEverywhereAtTheSameTime()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        var answers = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => service.GetInstalledAppsAsync())));

        Assert.All(answers, answer => Assert.Same(answers[0], answer));
    }

    [Fact]
    public async Task HoldsAnEmptyLibraryToo()
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));

        var service = CreateService();

        var first = await service.GetInstalledAppsAsync();
        var second = await service.GetInstalledAppsAsync();

        Assert.Empty(first);
        Assert.Same(first, second);
    }
}
