using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Hosting;

namespace Titanite.Bootstrap.Startup;

internal sealed class AppStartupService(
    IEnumerable<IStartupStep> steps,
    ILogger<AppStartupService> logger) : IAppStartupService
{
    private readonly Lock _starting = new();

    private Task? _run;

    private volatile bool _isRunning;
    private volatile IStartupStep? _current;

    public bool IsRunning => _isRunning;

    public string? Activity => _current?.Activity;

    public event Action? Changed;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        lock (_starting)
        {
            if (_run is null)
            {
                _isRunning = true;
                _run = Task.Run(() => RunStepsAsync(cancellationToken), CancellationToken.None);
            }

            return _run;
        }
    }

    private async Task RunStepsAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var step in steps)
            {
                _current = step;
                Changed?.Invoke();

                try
                {
                    await step.RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "{Step} did not finish. The application is starting anyway.", step.Name);
                }
            }
        }
        finally
        {
            _current = null;
            _isRunning = false;
            Changed?.Invoke();
        }
    }
}
