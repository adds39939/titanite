using Microsoft.Extensions.DependencyInjection;
using Titanite.UI.Services.Presentation;

namespace Titanite.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTitaniteUI(this IServiceCollection services)
    {
        services.AddTransient<IGameConfigurationPresenter, GameConfigurationPresenter>();
        services.AddTransient<IPresetsPresenter, PresetsPresenter>();
        services.AddTransient<IGameLibraryPresenter, GameLibraryPresenter>();

        return services;
    }
}
