namespace Titanite.Abstractions.Hosting;

public interface IAppStartupService
{
    bool IsRunning { get; }

    string? Activity { get; }

    event Action? Changed;

    Task RunAsync(CancellationToken cancellationToken = default);
}
