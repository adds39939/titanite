using FakeItEasy.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Steam.Client;
using Titanite.Steam.Launch;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class SteamLaunchOptionsServiceTests : IDisposable
{
    private const uint AppId = 2357570;

    private const string Document =
        "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"apps\"\n\t\t\t\t{\n\t\t\t\t\t\"2357570\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LaunchOptions\"\t\t\"PROTON_ENABLE_HDR=1 %command%\"\n" +
        "\t\t\t\t\t}\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    private const uint OtherAppId = 2138720;

    private const string InstallDocument =
        "\"InstallConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"CompatToolMapping\"\n\t\t\t\t{\n" +
        "\t\t\t\t\t\"0\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"name\"\t\t\"proton_experimental\"\n" +
        "\t\t\t\t\t\t\"config\"\t\t\"\"\n" +
        "\t\t\t\t\t\t\"priority\"\t\t\"75\"\n\t\t\t\t\t}\n" +
        "\t\t\t\t\t\"2138720\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"name\"\t\t\"GE-Proton11-3\"\n" +
        "\t\t\t\t\t\t\"config\"\t\t\"\"\n" +
        "\t\t\t\t\t\t\"priority\"\t\t\"250\"\n\t\t\t\t\t}\n" +
        "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    private readonly string _root = Directory.CreateTempSubdirectory("titanite-test-").FullName;

    private string ConfigPath => Path.Combine(_root, "userdata", "145618525", "config", "localconfig.vdf");

    private string InstallConfigPath => Path.Combine(_root, "config", "config.vdf");

    public SteamLaunchOptionsServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, Document);

        Directory.CreateDirectory(Path.GetDirectoryName(InstallConfigPath)!);
        File.WriteAllText(InstallConfigPath, InstallDocument);
    }

    private async Task<string?> MappingField(uint appId, string key) =>
        SteamConfigText.GetValue(
            await File.ReadAllTextAsync(InstallConfigPath),
            ["InstallConfigStore", "Software", "Valve", "Steam", "CompatToolMapping", appId.ToString(), key]);

    private static GameId Game(uint appId) => SteamIds.For(appId);

    private static Dictionary<GameId, LaunchOptions> Only(uint appId, string value) =>
        new() { [Game(appId)] = LaunchOptions.Parse(value) };

    private static Dictionary<GameId, string> OnlyTool(uint appId, string value) =>
        new() { [Game(appId)] = value };

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamLaunchOptionsService CreateService(
        ISteamClient client,
        ISteamClientBridge? bridge = null,
        ISteamDebugPort? debugPort = null) =>
        new(FakeSteamInstall.At(_root),
            client,
            bridge ?? Bridge(),
            A.Fake<ISteamDebuggingService>(),
            debugPort ?? DebugPort(listening: false),
            NullLogger<SteamLaunchOptionsService>.Instance);

    private static ISteamDebugPort DebugPort(bool listening)
    {
        var port = A.Fake<ISteamDebugPort>();

        A.CallTo(() => port.WaitUntilListeningAsync(A<TimeSpan>._, A<CancellationToken>._)).Returns(listening);

        return port;
    }

    private static ISteamClientBridge BridgeAnsweringAfterStart(ISteamClientSession session)
    {
        var bridge = A.Fake<ISteamClientBridge>();

        A.CallTo(() => bridge.ConnectAsync(A<CancellationToken>._))
            .ReturnsNextFromSequence(null, session);

        return bridge;
    }

    private static ISteamClient SteamClient(bool running = false, bool gameRunning = false)
    {
        var client = A.Fake<ISteamClient>();

        A.CallTo(() => client.IsRunning()).Returns(running);
        A.CallTo(() => client.IsGameRunning()).Returns(gameRunning);
        A.CallTo(() => client.Start()).Returns(true);
        A.CallTo(() => client.LaunchGame(A<uint>._)).Returns(true);

        return client;
    }

    private static ISteamClientBridge Bridge(ISteamClientSession? session = null)
    {
        var bridge = A.Fake<ISteamClientBridge>();

        A.CallTo(() => bridge.ConnectAsync(A<CancellationToken>._)).Returns(session);

        return bridge;
    }

    private static ISteamClientSession Session(
        Dictionary<uint, SteamAppDetails>? held = null,
        params uint[] refusing)
    {
        held ??= [];

        var session = A.Fake<ISteamClientSession>();

        A.CallTo(() => session.IsReadyAsync(A<CancellationToken>._)).Returns(true);

        A.CallTo(() => session.GetAppDetailsAsync(A<uint>._, A<CancellationToken>._))
            .ReturnsLazily((uint appId, CancellationToken _) => held.GetValueOrDefault(appId));

        A.CallTo(() => session.GetAppDetailsAsync(A<IReadOnlyCollection<uint>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<uint> appIds, CancellationToken _) =>
                (IReadOnlyDictionary<uint, SteamAppDetails>)appIds
                    .Where(held.ContainsKey)
                    .ToDictionary(appId => appId, appId => held[appId]));

        A.CallTo(() => session.SetLaunchOptionsAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((uint appId, string launchOptions, CancellationToken _) =>
                Accept(held, refusing, appId, details => details with { LaunchOptions = launchOptions }));

        A.CallTo(() => session.SetCompatToolAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((uint appId, string toolName, CancellationToken _) =>
                Accept(held, refusing, appId, details => details with
                {
                    CompatToolName = toolName.Length == 0 ? "proton_experimental" : toolName
                }));

        return session;
    }

    private static bool Accept(
        Dictionary<uint, SteamAppDetails> held,
        IReadOnlyCollection<uint> refusing,
        uint appId,
        Func<SteamAppDetails, SteamAppDetails> change)
    {
        if (refusing.Contains(appId))
        {
            return false;
        }

        held[appId] = change(held.GetValueOrDefault(appId) ?? new SteamAppDetails(string.Empty, string.Empty));

        return true;
    }

    private static ISteamClientSession ContrarySession()
    {
        var session = A.Fake<ISteamClientSession>();

        A.CallTo(() => session.GetAppDetailsAsync(A<IReadOnlyCollection<uint>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<uint> appIds, CancellationToken _) =>
                (IReadOnlyDictionary<uint, SteamAppDetails>)appIds.ToDictionary(
                    appId => appId,
                    _ => new SteamAppDetails("something else entirely", string.Empty)));

        A.CallTo(() => session.SetLaunchOptionsAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .Returns(true);

        A.CallTo(() => session.SetCompatToolAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .Returns(true);

        return session;
    }

    private static ISteamClientSession LaggingSession(int staleReads)
    {
        var session = A.Fake<ISteamClientSession>();
        var reads = 0;
        var held = string.Empty;

        A.CallTo(() => session.GetAppDetailsAsync(A<IReadOnlyCollection<uint>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<uint> appIds, CancellationToken _) =>
            {
                var compatTool = ++reads <= staleReads ? "proton_9" : held;

                return (IReadOnlyDictionary<uint, SteamAppDetails>)appIds.ToDictionary(
                    appId => appId,
                    _ => new SteamAppDetails(string.Empty, compatTool));
            });

        A.CallTo(() => session.SetLaunchOptionsAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .Returns(true);

        A.CallTo(() => session.SetCompatToolAsync(A<uint>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((uint _, string toolName, CancellationToken _) =>
            {
                held = toolName;

                return true;
            });

        return session;
    }

    private static IAssertConfiguration Connect(ISteamClientBridge bridge) =>
        A.CallTo(() => bridge.ConnectAsync(A<CancellationToken>._));

    private static IAssertConfiguration Wrote(ISteamClientSession session, uint appId, string launchOptions) =>
        A.CallTo(() => session.SetLaunchOptionsAsync(appId, launchOptions, A<CancellationToken>._));

    [Fact]
    public async Task ReadsWhatSteamHasStored()
    {
        var options = await CreateService(SteamClient()).GetAsync(Game(AppId));

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", options.Format());
    }

    [Fact]
    public async Task HandsTheChangeToARunningSteamWithoutClosingIt()
    {
        var client = SteamClient(running: true);
        var held = new Dictionary<uint, SteamAppDetails>();

        var result = await CreateService(client, bridge: Bridge(Session(held)))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.True(result.IsSuccess);
        A.CallTo(() => client.ShutdownAsync(A<TimeSpan>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => client.Start()).MustNotHaveHappened();
        Assert.Equal("DXVK_HDR=1 %command%", held[AppId].LaunchOptions);
    }

    [Fact]
    public async Task DoesNotTouchTheFileWhenSteamTakesTheChange()
    {
        var before = await File.ReadAllTextAsync(ConfigPath);

        await CreateService(SteamClient(running: true), bridge: Bridge(Session()))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(before, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task SendsLaunchOptionsAndProtonBuildThroughOneConnection()
    {
        var held = new Dictionary<uint, SteamAppDetails>();
        var bridge = Bridge(Session(held));

        var result = await CreateService(SteamClient(running: true), bridge: bridge)
            .SaveManyAsync(Only(AppId, "DXVK_HDR=1 %command%"), OnlyTool(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        Connect(bridge).MustHaveHappenedOnceExactly();
        Assert.Equal("DXVK_HDR=1 %command%", held[AppId].LaunchOptions);
        Assert.Equal("GE-Proton11-3", held[AppId].CompatToolName);
    }

    [Fact]
    public async Task SendsABatchOneGameAtATime()
    {
        var session = Session();

        await CreateService(SteamClient(running: true), bridge: Bridge(session))
            .SaveManyAsync(new Dictionary<GameId, LaunchOptions>
            {
                [Game(AppId)] = LaunchOptions.Parse("A=1 %command%"),
                [Game(AppId + 1)] = LaunchOptions.Parse("B=1 %command%"),
                [Game(AppId + 2)] = LaunchOptions.Parse("C=1 %command%")
            });

        Wrote(session, AppId, "A=1 %command%").MustHaveHappenedOnceExactly()
            .Then(Wrote(session, AppId + 1, "B=1 %command%").MustHaveHappenedOnceExactly())
            .Then(Wrote(session, AppId + 2, "C=1 %command%").MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task ClosesTheConnectionWhenItIsDone()
    {
        var session = Session();

        await CreateService(SteamClient(running: true), bridge: Bridge(session))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        A.CallTo(() => session.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SavesWhileAGameIsRunning()
    {
        var client = SteamClient(running: true, gameRunning: true);

        var result = await CreateService(client, bridge: Bridge(Session()))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.True(result.IsSuccess);
        A.CallTo(() => client.ShutdownAsync(A<TimeSpan>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReportsWhatSteamWouldNotAccept()
    {
        var result = await CreateService(
                SteamClient(running: true),
                bridge: Bridge(Session(refusing: AppId)))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.False(result.IsSuccess);
        Assert.Equal(LaunchOptionsSaveStatus.WriteFailed, result.Status);
        Assert.Contains(AppId.ToString(), result.Message);
    }

    [Fact]
    public async Task ChecksWhatSteamHoldsAfterwards()
    {
        var result = await CreateService(SteamClient(running: true), bridge: Bridge(ContrarySession()))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.False(result.IsSuccess);
        Assert.Equal(LaunchOptionsSaveStatus.WriteFailed, result.Status);
    }

    [Fact]
    public async Task WaitsForSteamToCatchUpWithItselfBeforeCallingItAMismatch()
    {
        var session = LaggingSession(staleReads: 2);

        var result = await CreateService(SteamClient(running: true), bridge: Bridge(session))
            .SaveManyAsync(new Dictionary<GameId, LaunchOptions>(), OnlyTool(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        A.CallTo(() => session.GetAppDetailsAsync(A<IReadOnlyCollection<uint>>._, A<CancellationToken>._))
            .MustHaveHappened(3, Times.OrMore);
    }

    [Fact]
    public async Task DoesNotCallASteamChosenBuildAMismatch()
    {
        var held = new Dictionary<uint, SteamAppDetails>();

        var result = await CreateService(SteamClient(running: true), bridge: Bridge(Session(held)))
            .SaveManyAsync(new Dictionary<GameId, LaunchOptions>(), OnlyTool(AppId, string.Empty));

        Assert.True(result.IsSuccess);
        Assert.Equal("proton_experimental", held[AppId].CompatToolName);
    }

    [Fact]
    public async Task ReadsWhatTheRunningSteamHoldsRatherThanTheFile()
    {
        var held = new Dictionary<uint, SteamAppDetails>
        {
            [AppId] = new("CHANGED_IN_STEAM=1 %command%", string.Empty)
        };

        var options = await CreateService(SteamClient(running: true), bridge: Bridge(Session(held)))
            .GetAsync(Game(AppId));

        Assert.Equal("CHANGED_IN_STEAM=1 %command%", options.Format());
    }

    [Fact]
    public async Task FallsBackToTheFileWhenSteamDoesNotAnswerForTheGame()
    {
        var options = await CreateService(SteamClient(running: true), bridge: Bridge(Session()))
            .GetAsync(Game(AppId));

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", options.Format());
    }

    [Fact]
    public async Task ReadsAWholeBatchThroughOneConnection()
    {
        var session = Session(new Dictionary<uint, SteamAppDetails>
        {
            [AppId] = new("A=1 %command%", string.Empty),
            [AppId + 1] = new("B=1 %command%", string.Empty)
        });

        var bridge = Bridge(session);

        var found = await CreateService(SteamClient(running: true), bridge: bridge)
            .GetManyAsync([Game(AppId), Game(AppId + 1)]);

        Connect(bridge).MustHaveHappenedOnceExactly();
        A.CallTo(() => session.GetAppDetailsAsync(A<IReadOnlyCollection<uint>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.Equal("A=1 %command%", found[Game(AppId)].Format());
        Assert.Equal("B=1 %command%", found[Game(AppId + 1)].Format());
    }

    [Fact]
    public async Task ReadsTheFileAgainOnceSteamRewritesIt()
    {
        var service = CreateService(SteamClient());

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", (await service.GetAsync(Game(AppId))).Format());

        File.WriteAllText(ConfigPath, Document.Replace("PROTON_ENABLE_HDR=1", "MANGOHUD=1"));
        File.SetLastWriteTimeUtc(ConfigPath, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("MANGOHUD=1 %command%", (await service.GetAsync(Game(AppId))).Format());
    }

    [Fact]
    public async Task AnswersForEveryGameAskedAbout()
    {
        var found = await CreateService(SteamClient()).GetManyAsync([Game(AppId), Game(12210)]);

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", found[Game(AppId)].Format());
        Assert.Equal(string.Empty, found[Game(12210)].Format());
    }

    [Fact]
    public async Task SaysSavingWillStartSteamWhileItIsNotRunning()
    {
        var availability = await CreateService(SteamClient()).GetAvailabilityAsync();

        Assert.Equal(AvailabilityStatus.Available, availability.Status);
        Assert.Equal(
            "Steam is not running — saving will start Steam and apply the changes.",
            availability.Explanation);
    }

    [Fact]
    public async Task StartsSteamToSaveWhenItIsNotRunning()
    {
        var client = SteamClient();
        var session = Session(new Dictionary<uint, SteamAppDetails>
        {
            [AppId] = new(string.Empty, string.Empty)
        });

        var result = await CreateService(client, BridgeAnsweringAfterStart(session), DebugPort(listening: true))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.Saved, result.Status);
        A.CallTo(() => client.Start()).MustHaveHappenedOnceExactly();
        Wrote(session, AppId, "DXVK_HDR=1 %command%").MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WaitsForAStartedSteamToFinishSigningInBeforeWriting()
    {
        var session = Session(new Dictionary<uint, SteamAppDetails>
        {
            [AppId] = new(string.Empty, string.Empty)
        });

        A.CallTo(() => session.IsReadyAsync(A<CancellationToken>._)).ReturnsNextFromSequence(false, true);

        var bridge = A.Fake<ISteamClientBridge>();

        A.CallTo(() => bridge.ConnectAsync(A<CancellationToken>._)).ReturnsNextFromSequence(null, session, session);

        var result = await CreateService(SteamClient(), bridge, DebugPort(listening: true))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.Saved, result.Status);
        A.CallTo(() => session.IsReadyAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        Wrote(session, AppId, "DXVK_HDR=1 %command%").MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DoesNotStartSteamThatIsRunningButNotAnswering()
    {
        var client = SteamClient(running: true);

        var result = await CreateService(client, debugPort: DebugPort(listening: true))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.LauncherUnavailable, result.Status);
        A.CallTo(() => client.Start()).MustNotHaveHappened();
    }

    [Fact]
    public async Task SaysSoWhenSteamCannotBeStarted()
    {
        var client = SteamClient();

        A.CallTo(() => client.Start()).Returns(false);

        var result = await CreateService(client).SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.LauncherUnavailable, result.Status);
        Assert.Equal("Steam could not be started. Nothing was changed.", result.Message);
    }

    [Fact]
    public async Task SaysSoWhenAStartedSteamNeverAnswers()
    {
        var result = await CreateService(SteamClient())
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.LauncherUnavailable, result.Status);
        Assert.Contains("never answered", result.Message);
    }

    [Fact]
    public async Task SaysTheChangeGoesStraightInWhenSteamWillTakeIt()
    {
        var availability = await CreateService(SteamClient(running: true), bridge: Bridge(Session()))
            .GetAvailabilityAsync();

        Assert.Equal(AvailabilityStatus.Available, availability.Status);
        Assert.Null(availability.Explanation);
    }

    [Fact]
    public async Task LeavesTheHostAloneWhileSteamIsAnswering()
    {
        var client = SteamClient(running: true);

        await CreateService(client, bridge: Bridge(Session())).GetAvailabilityAsync();

        A.CallTo(() => client.IsRunning()).MustNotHaveHappened();
    }

    [Fact]
    public async Task SaysNothingCanBeSavedWhenSteamIsNotAnswering()
    {
        var availability = await CreateService(SteamClient(running: true)).GetAvailabilityAsync();

        Assert.Equal(AvailabilityStatus.Blocked, availability.Status);
        Assert.Contains("is not answering", availability.Explanation);
    }

    [Fact]
    public async Task SavesWhileAGameIsRunningWhenSteamAnswers()
    {
        var availability = await CreateService(
                SteamClient(running: true, gameRunning: true),
                bridge: Bridge(Session()))
            .GetAvailabilityAsync();

        Assert.Equal(AvailabilityStatus.Available, availability.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WritesNothingWhenSteamCannotBeReached(bool steamRunning)
    {
        var before = await File.ReadAllTextAsync(ConfigPath);

        var result = await CreateService(SteamClient(running: steamRunning))
            .SaveAsync(Game(AppId), LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(LaunchOptionsSaveStatus.LauncherUnavailable, result.Status);
        Assert.Equal(before, await File.ReadAllTextAsync(ConfigPath));
    }
}
