using Microsoft.Extensions.DependencyInjection;

namespace Titanite.Bootstrap;

public static class ServiceDecoration
{
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var registered = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TService))
                         ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered.");

        services.Remove(registered);

        services.Add(new ServiceDescriptor(
            typeof(TService),
            provider => ActivatorUtilities.CreateInstance<TDecorator>(
                provider,
                (TService)Instantiate(provider, registered)),
            registered.Lifetime));

        return services;
    }

    private static object Instantiate(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return factory(provider);
        }

        return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
