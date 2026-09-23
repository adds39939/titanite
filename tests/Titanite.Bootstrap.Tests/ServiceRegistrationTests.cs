using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Titanite.Abstractions.Hosting;
using Titanite.Platform.Updates;

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

    [Fact]
    public void TalksToGitHubAsTitanite()
    {
        using var provider = WithLogging().AddTitanite().BuildServiceProvider();

        var version = provider.GetRequiredService<IApplicationInfo>().Version;
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GitHubReleaseUpdater));

        Assert.Equal($"Titanite/{version}", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal("application/vnd.github+json", client.DefaultRequestHeaders.Accept.ToString());
        Assert.Equal(["2022-11-28"], client.DefaultRequestHeaders.GetValues("X-GitHub-Api-Version"));
    }

    [Fact]
    public void DownloadsUpdatesIntoTheCache()
    {
        using var provider = WithLogging().AddTitanite().BuildServiceProvider();

        var directory = provider.GetRequiredService<IOptions<UpdaterOptions>>().Value.DownloadDirectory;

        Assert.Equal(ServiceCollectionExtensions.UpdateDownloadDirectory(), directory);
        Assert.EndsWith(Path.Combine("titanite", "updates"), directory);
    }

    private static ServiceCollection WithLogging()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(A.Fake<IAppEnvironment>());

        return services;
    }
}
