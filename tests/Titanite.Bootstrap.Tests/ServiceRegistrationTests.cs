using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Hosting;

namespace Titanite.Bootstrap.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void EveryServiceCanBeBuilt()
    {
        var services = WithLogging().AddTitanite();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider);
    }

    [Fact]
    public void EveryServiceResolves()
    {
        var services = WithLogging().AddTitanite();

        using var provider = services.BuildServiceProvider();

        var resolvable = services.Where(service =>
            !service.ServiceType.IsGenericTypeDefinition &&
            (service.ServiceType.IsInterface || service.ImplementationFactory is not null));

        Assert.All(resolvable, service => Assert.NotNull(provider.GetService(service.ServiceType)));
    }

    [Fact]
    public void OffersItsCustomSchemeHandlersToTheHost()
    {
        using var provider = WithLogging().AddTitanite().BuildServiceProvider();

        Assert.Contains(
            provider.GetServices<ICustomSchemeHandler>(),
            handler => handler.Scheme == "artwork");
    }

    private static ServiceCollection WithLogging()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services;
    }
}
