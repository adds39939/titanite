namespace Titanite.Abstractions.Hosting;

public interface IStartupStep
{
    string Name { get; }

    Task RunAsync(CancellationToken cancellationToken = default);
}
