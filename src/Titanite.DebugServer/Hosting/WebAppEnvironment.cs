using Titanite.Abstractions.Hosting;

namespace Titanite.DebugServer.Hosting;

internal sealed class WebAppEnvironment(IHostEnvironment environment) : IAppEnvironment
{
    public bool IsDevelopment => environment.IsDevelopment();
}
