namespace Titanite.Abstractions.Hosting;

public interface IAppStartupService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
