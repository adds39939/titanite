using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Processes;
using Titanite.Platform.Processes;

namespace Titanite.Bootstrap.Tests;

public class HostProcessesTests
{
    [Fact]
    public void ReachesTheHostThroughFlatpakInsideTheSandbox() =>
        Assert.IsType<FlatpakHostProcesses>(Resolve(sandboxed: true));

    [Fact]
    public void ReachesTheHostDirectlyOutsideTheSandbox() =>
        Assert.IsType<NativeHostProcesses>(Resolve(sandboxed: false));

    private static IHostProcesses Resolve(bool sandboxed)
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddHostProcesses(sandboxed);

        return services.BuildServiceProvider().GetRequiredService<IHostProcesses>();
    }
}
