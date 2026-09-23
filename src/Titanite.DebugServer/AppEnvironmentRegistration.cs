using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Launchers;
using Titanite.Bootstrap;

namespace Titanite.DebugServer;

internal static class AppEnvironmentRegistration
{
    public static IServiceCollection AddAppEnvironment(this IServiceCollection services) =>
        services
            .AddSingleton<IAppEnvironment, WebAppEnvironment>()
            .AddSingleton<IAppLifetime, WebAppLifetime>()
            .Decorate<IGameArtwork, HttpArtworkService>();
}
