using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Hosting;

namespace Titanite.Bootstrap;

public static class ServiceProviderExtensions
{
    public static Task StartTitaniteAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default) =>
        services.GetRequiredService<IAppStartupService>().RunAsync(cancellationToken);
}
