using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Hosting;

namespace Titanite.App;

internal static class AppEnvironmentRegistration
{
    public static IServiceCollection AddAppEnvironment(this IServiceCollection services) =>
        services
            .AddSingleton<IAppEnvironment, PhotinoAppEnvironment>()
            .AddSingleton<PhotinoAppLifetime>()
            .AddSingleton<IAppLifetime>(provider => provider.GetRequiredService<PhotinoAppLifetime>());
}
