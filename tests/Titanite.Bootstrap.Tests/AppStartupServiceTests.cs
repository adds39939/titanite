using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Abstractions.Hosting;
using Titanite.Bootstrap.Startup;

namespace Titanite.Bootstrap.Tests;

public sealed class AppStartupServiceTests
{
    private readonly List<string> _ran = [];

    private AppStartupService CreateService(params IStartupStep[] steps) =>
        new(steps, NullLogger<AppStartupService>.Instance);

    [Fact]
    public async Task RunsEveryStep()
    {
        await CreateService(Step("first"), Step("second")).RunAsync();

        Assert.Equal(["first", "second"], _ran);
    }

    [Fact]
    public async Task RunsThemInTheOrderTheyWereRegistered()
    {
        await CreateService(Step("second"), Step("first")).RunAsync();

        Assert.Equal(["second", "first"], _ran);
    }

    [Fact]
    public async Task CarriesOnWhenAStepFails()
    {
        await CreateService(Step("first"), Failing("second"), Step("third")).RunAsync();

        Assert.Equal(["first", "third"], _ran);
    }

    [Fact]
    public async Task HasNothingToDoWithNoStepsAtAll()
    {
        await CreateService().RunAsync();

        Assert.Empty(_ran);
    }

    [Fact]
    public async Task HandsEachStepTheTokenItWasGiven()
    {
        using var stopping = new CancellationTokenSource();

        var step = A.Fake<IStartupStep>();

        await CreateService(step).RunAsync(stopping.Token);

        A.CallTo(() => step.RunAsync(stopping.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RunsOnlyOnceHoweverOftenItIsStarted()
    {
        var service = CreateService(Step("first"));

        await Task.WhenAll(service.RunAsync(), service.RunAsync());

        Assert.Equal(["first"], _ran);
    }

    [Fact]
    public async Task SaysWhatItIsDoingWithoutHoldingUpWhoeverStartedIt()
    {
        var release = new TaskCompletionSource();
        var step = A.Fake<IStartupStep>();

        A.CallTo(() => step.Activity).Returns("Connecting to Steam…");
        A.CallTo(() => step.RunAsync(A<CancellationToken>._)).Returns(release.Task);

        var service = CreateService(step);
        var run = service.RunAsync();

        Assert.False(run.IsCompleted);
        Assert.True(service.IsRunning);
        Assert.Equal("Connecting to Steam…", await ActivityOnceStarted(service));

        release.SetResult();
        await run;

        Assert.False(service.IsRunning);
        Assert.Null(service.Activity);
    }

    [Fact]
    public async Task TellsListenersOnceItHasFinished()
    {
        var service = CreateService(Step("first"));
        var finished = false;

        service.Changed += () => finished = !service.IsRunning;

        await service.RunAsync();

        Assert.True(finished);
    }

    [Fact]
    public void RunsTheLauncherCheckBeforeThePresetCheck()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(A.Fake<IAppEnvironment>());

        using var provider = services.AddTitanite().BuildServiceProvider();

        var names = provider.GetServices<IStartupStep>().Select(step => step.Name);

        Assert.Equal(["The launcher debugging check", "The preset check"], names);
    }

    private static async Task<string?> ActivityOnceStarted(AppStartupService service)
    {
        for (var attempt = 0; attempt < 100 && service.Activity is null; attempt++)
        {
            await Task.Delay(10);
        }

        return service.Activity;
    }

    private IStartupStep Step(string name)
    {
        var step = A.Fake<IStartupStep>();

        A.CallTo(() => step.Name).Returns(name);
        A.CallTo(() => step.RunAsync(A<CancellationToken>._)).Invokes(() => _ran.Add(name));

        return step;
    }

    private static IStartupStep Failing(string name)
    {
        var step = A.Fake<IStartupStep>();

        A.CallTo(() => step.Name).Returns(name);
        A.CallTo(() => step.RunAsync(A<CancellationToken>._)).Throws(new IOException("config.vdf is locked"));

        return step;
    }
}
