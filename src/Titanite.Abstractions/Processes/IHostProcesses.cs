namespace Titanite.Abstractions.Processes;

public interface IHostProcesses
{
    bool IsRunning(string processName);

    bool AnyCommandLineContains(string fragment);

    bool Start(string fileName, IReadOnlyList<string> arguments);

    Task<bool> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
