using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Processes;
using Titanite.Steam.Client;

namespace Titanite.Steam.Tests;

public class SteamClientLaunchTests
{
    private readonly IHostProcesses _host = A.Fake<IHostProcesses>();

    [Fact]
    public void StartsSteamOnTheHost()
    {
        A.CallTo(() => _host.Start("steam", A<IReadOnlyList<string>>._)).Returns(true);

        Assert.True(Client().Start());
        A.CallTo(() => _host.Start("steam", A<IReadOnlyList<string>>.That.IsEmpty()))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void LaunchesAGameThroughSteamsOwnAddress()
    {
        Client().LaunchGame(440);

        A.CallTo(() => _host.Start(
                "steam",
                A<IReadOnlyList<string>>.That.IsSameSequenceAs("steam://rungameid/440")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AsksARunningSteamToShutDown()
    {
        A.CallTo(() => _host.IsRunning("steam")).ReturnsNextFromSequence(true, false);
        A.CallTo(() => _host.Start("steam", A<IReadOnlyList<string>>._)).Returns(true);

        Assert.True(await Client().ShutdownAsync(TimeSpan.FromSeconds(5)));
        A.CallTo(() => _host.Start(
                "steam",
                A<IReadOnlyList<string>>.That.IsSameSequenceAs("-shutdown")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void ReportsFailureWhenTheHostCannotStartSteam()
    {
        A.CallTo(() => _host.Start("steam", A<IReadOnlyList<string>>._)).Returns(false);

        Assert.False(Client().Start());
    }

    [Fact]
    public void LooksForSteamByItsProcessName()
    {
        A.CallTo(() => _host.IsRunning("steam")).Returns(true);

        Assert.True(Client().IsRunning());
    }

    [Fact]
    public void RecognisesAGameByTheLaunchMarkerSteamGivesIt()
    {
        A.CallTo(() => _host.AnyCommandLineContains("SteamLaunch AppId=")).Returns(true);

        Assert.True(Client().IsGameRunning());
    }

    private SteamClient Client() => new(NullLogger<SteamClient>.Instance, _host);
}
