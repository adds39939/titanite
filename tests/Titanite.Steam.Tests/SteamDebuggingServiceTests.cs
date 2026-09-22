using FakeItEasy.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Steam.Client;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class SteamDebuggingServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-debugging-").FullName;

    private string MarkerPath => Path.Combine(_root, SteamDebuggingService.MarkerFileName);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamDebuggingService CreateService(
        ISteamClient? client = null,
        ISteamDebugPort? port = null) =>
        CreateServiceFor(_root, client, port);

    private static SteamDebuggingService CreateServiceWithoutSteam() => CreateServiceFor(null);

    private static SteamDebuggingService CreateServiceFor(
        string? steamRoot,
        ISteamClient? client = null,
        ISteamDebugPort? port = null) =>
        new(FakeSteamInstall.At(steamRoot),
            client ?? SteamClient(),
            port ?? A.Fake<ISteamDebugPort>(),
            NullLogger<SteamDebuggingService>.Instance);

    private static ISteamClient SteamClient(
        bool running = false,
        bool gameRunning = false,
        bool closesOnRequest = true,
        Action? onShutdown = null)
    {
        var client = A.Fake<ISteamClient>();
        var hasExited = false;

        A.CallTo(() => client.IsRunning()).ReturnsLazily(() => running && !hasExited);
        A.CallTo(() => client.IsGameRunning()).Returns(gameRunning);
        A.CallTo(() => client.Start()).Returns(true);
        A.CallTo(() => client.LaunchGame(A<uint>._)).Returns(true);

        A.CallTo(() => client.ShutdownAsync(A<TimeSpan>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                onShutdown?.Invoke();
                hasExited = closesOnRequest;

                return closesOnRequest;
            });

        return client;
    }

    private static ISteamDebugPort DebugPortComingUp()
    {
        var port = A.Fake<ISteamDebugPort>();
        var waitedFor = false;

        A.CallTo(() => port.IsListeningAsync(A<CancellationToken>._)).ReturnsLazily(() => waitedFor);

        A.CallTo(() => port.WaitUntilListeningAsync(A<TimeSpan>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                waitedFor = true;

                return true;
            });

        return port;
    }

    private static IAssertConfiguration Shutdown(ISteamClient client) =>
        A.CallTo(() => client.ShutdownAsync(A<TimeSpan>._, A<CancellationToken>._));

    private static IAssertConfiguration Start(ISteamClient client) => A.CallTo(() => client.Start());

    private static IAssertConfiguration Wait(ISteamDebugPort port) =>
        A.CallTo(() => port.WaitUntilListeningAsync(A<TimeSpan>._, A<CancellationToken>._));

    [Fact]
    public async Task WritesTheFileWhenItIsMissing()
    {
        var outcome = await CreateService().EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.EnabledPendingStart, outcome);
        Assert.True(File.Exists(MarkerPath));
        Assert.Equal(string.Empty, await File.ReadAllTextAsync(MarkerPath));
    }

    [Fact]
    public async Task DoesNothingAtAllWhenTheFileIsAlreadyThere()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var client = SteamClient(running: true);

        var outcome = await CreateService(client).EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.AlreadyEnabled, outcome);
        Shutdown(client).MustNotHaveHappened();
        Start(client).MustNotHaveHappened();
    }

    [Fact]
    public async Task RestartsARunningSteamSoTheFileIsRead()
    {
        var client = SteamClient(running: true);

        var outcome = await CreateService(client, DebugPortComingUp()).EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.EnabledAndRestarted, outcome);
        Shutdown(client).MustHaveHappenedOnceExactly();
        Start(client).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DoesNotStartASteamThatWasNotRunning()
    {
        var client = SteamClient(running: false);

        var outcome = await CreateService(client).EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.EnabledPendingStart, outcome);
        Shutdown(client).MustNotHaveHappened();
        Start(client).MustNotHaveHappened();
    }

    [Fact]
    public async Task WillNotCloseSteamOutFromUnderAGame()
    {
        var client = SteamClient(running: true, gameRunning: true);

        var outcome = await CreateService(client).EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.EnabledPendingRestart, outcome);
        Assert.True(File.Exists(MarkerPath));
        Shutdown(client).MustNotHaveHappened();
        Start(client).MustNotHaveHappened();
    }

    [Fact]
    public async Task DoesNotStartASecondSteamWhenTheFirstWillNotClose()
    {
        var client = SteamClient(running: true, closesOnRequest: false);

        var outcome = await CreateService(client).EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.EnabledPendingRestart, outcome);
        Start(client).MustNotHaveHappened();
    }

    [Fact]
    public async Task WritesTheFileBeforeClosingSteam()
    {
        var client = SteamClient(running: true, onShutdown: () => Assert.True(File.Exists(MarkerPath)));

        await CreateService(client).EnsureEnabledAsync();

        Shutdown(client).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WaitsForSteamToStartAnsweringBeforeSayingItIsDone()
    {
        var port = DebugPortComingUp();

        await CreateService(SteamClient(running: true), port).EnsureEnabledAsync();

        Wait(port).MustHaveHappened();
    }

    [Fact]
    public async Task DoesNotWaitOnASteamThatWasNeverRestarted()
    {
        var port = A.Fake<ISteamDebugPort>();

        await CreateService(SteamClient(running: false), port).EnsureEnabledAsync();

        Wait(port).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReportsAMissingSteamInsteadOfWritingSomewhereElse()
    {
        var outcome = await CreateServiceWithoutSteam().EnsureEnabledAsync();

        Assert.Equal(SteamDebuggingOutcome.NoSteamInstall, outcome);
        Assert.False(File.Exists(MarkerPath));
    }
}
