using Microsoft.Extensions.Logging.Abstractions;
using Titanite.Platform.Processes;

namespace Titanite.Platform.Tests.Processes;

public class NativeHostProcessesTests
{
    private readonly NativeHostProcesses _processes = new(NullLogger<NativeHostProcesses>.Instance);

    [Fact]
    public async Task WaitsForACommandThatSucceeds() =>
        Assert.True(await _processes.RunAsync("sh", ["-c", "exit 0"]));

    [Fact]
    public async Task ReportsACommandThatFails() =>
        Assert.False(await _processes.RunAsync("sh", ["-c", "echo nope >&2; exit 3"]));

    [Fact]
    public async Task ReportsACommandThatDoesNotExist() =>
        Assert.False(await _processes.RunAsync("titanite-no-such-command", []));
}
