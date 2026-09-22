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
    public void RunsTheLauncherCheckBeforeThePresetCheck()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        using var provider = services.AddTitanite().BuildServiceProvider();

        var names = provider.GetServices<IStartupStep>().Select(step => step.Name);

        Assert.Equal(["The launcher debugging check", "The preset check"], names);
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
