using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Hosting;

namespace Titanite.App.Hosting;

internal static class AppEnvironmentRegistration
{
    public static IServiceCollection AddAppEnvironment(this IServiceCollection services) =>
        services
            .AddSingleton<IAppEnvironment, PhotinoAppEnvironment>()
            .AddSingleton<IAppLifetime, PhotinoAppLifetime>()
            .AddSingleton<IInterfaceScaler, PhotinoInterfaceScaler>();
}
