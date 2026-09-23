using Titanite.Abstractions.Hosting;

namespace Titanite.DebugServer;

internal sealed class WebAppEnvironment(IHostEnvironment environment) : IAppEnvironment
{
    public bool IsDevelopment => environment.IsDevelopment();
}
