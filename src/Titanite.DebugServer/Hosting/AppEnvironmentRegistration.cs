using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Launchers;
using Titanite.Bootstrap;
using Titanite.DebugServer.Launchers;

namespace Titanite.DebugServer.Hosting;

internal static class AppEnvironmentRegistration
{
    public static IServiceCollection AddAppEnvironment(this IServiceCollection services) =>
        services
            .AddSingleton<IAppEnvironment, WebAppEnvironment>()
            .AddSingleton<IAppLifetime, WebAppLifetime>()
            .AddScoped<IInterfaceScaler, WebInterfaceScaler>()
            .Decorate<IGameArtwork, HttpArtworkService>();
}
