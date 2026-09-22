using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Storage.Presets;

namespace Titanite.Storage.Tests;

public sealed class PresetMigrationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-preset-migration-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string LegacyProfile = """
        {
          "LaunchOptions": "PROTON_ENABLE_HDR=1 mangohud %command% -novid",
          "LinkedApps": [
            2357570,
            993090
          ]
        }
        """;

    private const string CurrentProfile = """
        {
          "LaunchOptions": {
            "Environment": [ { "Name": "MANGOHUD", "Value": "1" } ],
            "Wrapper": [],
            "HasCommandPlaceholder": true,
            "Arguments": []
          },
          "LinkedGames": [ "steam:620" ]
        }
        """;

    private PresetService CreateService() =>
        new(TitaniteStorage.At(_root),
            A.Fake<ILaunchOptionsStore>(),
            A.Fake<IGameLauncher>(),
            NullLogger<PresetService>.Instance);

    private void WriteProfile(string json)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "profile.json"), json);
    }

    [Fact]
    public async Task CarriesTheOldGlobalProfileIntoTheGlobalPreset()
    {
        WriteProfile(CurrentProfile);

        var global = await CreateService().GetAsync(PresetId.Global);

        Assert.Equal("MANGOHUD=1 %command%", global.Options.Format());
    }

    [Fact]
    public async Task CarriesTheGamesThatFollowedItOntoTheGlobalPreset()
    {
        WriteProfile(CurrentProfile);

        Assert.Equal(PresetId.Global, await CreateService().AppliedToAsync(new GameId("steam", "620")));
    }

    [Fact]
    public async Task ReadsTheOldestProfileShapeToo()
    {
        WriteProfile(LegacyProfile);

        var service = CreateService();

        Assert.Equal(
            "PROTON_ENABLE_HDR=1 mangohud %command% -novid",
            (await service.GetAsync(PresetId.Global)).Options.Format());

        Assert.Equal(
            [new GameId("steam", "2357570"), new GameId("steam", "993090")],
            await service.GamesUsingAsync(PresetId.Global));
    }

    [Fact]
    public async Task LeavesTheOldFileAloneUntilSomethingChanges()
    {
        WriteProfile(CurrentProfile);

        await CreateService().GetAllAsync();

        Assert.False(File.Exists(Path.Combine(_root, "presets.json")));
        Assert.True(File.Exists(Path.Combine(_root, "profile.json")));
    }

    [Fact]
    public async Task WritesThePresetFileOnceSomethingChanges()
    {
        WriteProfile(CurrentProfile);

        await CreateService().CreateAsync("Handheld");

        var written = await File.ReadAllTextAsync(Path.Combine(_root, "presets.json"));

        Assert.Contains("\"Global\"", written);
        Assert.Contains("\"Handheld\"", written);
        Assert.Contains("steam:620", written);
    }

    [Fact]
    public async Task PrefersThePresetFileOnceItExists()
    {
        WriteProfile(CurrentProfile);

        await CreateService().SaveAndApplyAsync(
            Preset.Global with { Options = LaunchOptions.Parse("DXVK_HUD=1 %command%") });

        Assert.Equal("DXVK_HUD=1 %command%", (await CreateService().GetAsync(PresetId.Global)).Options.Format());
    }

    [Fact]
    public async Task PutsGlobalBackIfTheFileHasLostIt()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "presets.json"), """{ "Presets": [] }""");

        Assert.Equal(["Global"], (await CreateService().GetAllAsync()).Select(preset => preset.Name));
    }

    [Fact]
    public async Task StartsFreshWhenThePresetFileIsUnreadable()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "presets.json"), "not json");

        Assert.Equal(["Global"], (await CreateService().GetAllAsync()).Select(preset => preset.Name));
    }
}
