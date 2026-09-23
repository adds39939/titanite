namespace Titanite.Abstractions.Hosting;

public interface IStartupStep
{
    string Name { get; }

    string Activity { get; }

    Task RunAsync(CancellationToken cancellationToken = default);
}
