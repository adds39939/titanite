using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Cpu;
using Titanite.Abstractions.Desktop;
using Titanite.Abstractions.Hosting;
using Titanite.Bootstrap.Startup;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Abstractions.Processes;
using Titanite.Abstractions.Settings;
using Titanite.Abstractions.Updates;
using Titanite.Catalog;
using Titanite.Platform.Cpu;
using Titanite.Platform.Desktop;
using Titanite.Platform.Processes;
using Titanite.Platform.Updates;
using Titanite.Steam.Artwork;
using Titanite.Steam.Client;
using Titanite.Steam.Launch;
using Titanite.Steam.Library;
using Titanite.Steam.Proton;
using Titanite.Storage;
using Titanite.Storage.Presets;
using Titanite.Storage.Settings;

namespace Titanite.Bootstrap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTitanite(this IServiceCollection services)
    {
        services.AddSingleton(provider => new YamlSettingCatalogReader(
                YamlSettingCatalogReader.DefaultDirectory,
                provider.GetRequiredService<ILogger<YamlSettingCatalogReader>>())
            .Read());

        services.AddSingleton<ISteamInstallLocator, SteamInstallLocator>();
        services.AddSingleton<SteamLibraryService>();
        services.AddSingleton<ISteamLibraryService>(provider => provider.GetRequiredService<SteamLibraryService>());
        services.AddSingleton<IGameLibrary>(provider => provider.GetRequiredService<SteamLibraryService>());

        services.AddSingleton<SteamLibraryCacheArtworkService>();
        services.AddSingleton<IArtworkSource>(provider =>
            provider.GetRequiredService<SteamLibraryCacheArtworkService>());
        services.AddSingleton<IArtworkSource, SteamCdnArtworkService>();
        services.AddSingleton<IGameArtwork, FallbackArtworkService>();
        services.AddSingleton<ICustomSchemeHandler>(provider =>
            provider.GetRequiredService<SteamLibraryCacheArtworkService>());

        services.AddSingleton<ProtonToolService>();
        services.AddSingleton<IProtonToolService>(provider => provider.GetRequiredService<ProtonToolService>());
        services.AddSingleton<ICompatibilityTools>(provider => provider.GetRequiredService<ProtonToolService>());

        services.AddSingleton<ICpuTopologyService, LinuxCpuTopologyService>();
        services.AddSingleton<IFileManagerService, XdgFileManagerService>();
        services.AddSingleton<IBrowserService, XdgBrowserService>();
        services.AddHostProcesses(FlatpakSandbox.IsActive);
        services.AddSingleton<ITitaniteStorage, TitaniteStorage>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();

        services.AddSingleton<ISteamClient, SteamClient>();
        services.AddSingleton<IGameLauncher, SteamLauncher>();
        services.AddSingleton<ISteamDebugPort, SteamDebugPort>();
        services.AddSingleton<ISteamClientBridge, SteamClientBridge>();
        services.AddSingleton<ISteamDebuggingService, SteamDebuggingService>();

        services.AddSingleton<SteamLaunchOptionsService>();
        services.AddSingleton<ILaunchOptionsStore>(provider =>
            provider.GetRequiredService<SteamLaunchOptionsService>());
        services.AddSingleton<ILauncherAvailabilityProbe>(provider =>
            provider.GetRequiredService<SteamLaunchOptionsService>());
        services.AddSingleton<IGameLauncherAvailabilityWatcher, SteamAvailabilityWatcher>();
        services.AddSingleton<IGameConfigurationWatcher, SteamConfigurationWatcher>();
        services.AddSingleton<IPresetReconciler, PresetReconciler>();

        services.AddSingleton<IApplicationInfo, ApplicationInfo>();
        services.Configure<UpdaterOptions>(options => options.DownloadDirectory = UpdateDownloadDirectory());
        services.AddHttpClient<GitHubReleaseUpdater>((provider, client) =>
        {
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(
                $"Titanite/{provider.GetRequiredService<IApplicationInfo>().Version}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        services.AddTransient<IAppUpdater>(provider => provider.GetRequiredService<GitHubReleaseUpdater>());
        services.AddSingleton<IAppStartupService, AppStartupService>();
        services.AddSingleton<IStartupStep, LauncherDebuggingStep>();
        services.AddSingleton<IStartupStep, PresetReconciliationStep>();

        return services;
    }

    internal static string UpdateDownloadDirectory()
    {
        var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");

        if (string.IsNullOrWhiteSpace(cache))
        {
            cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        }

        return Path.Combine(cache, "titanite", "updates");
    }

    internal static IServiceCollection AddHostProcesses(this IServiceCollection services, bool sandboxed) =>
        sandboxed
            ? services.AddSingleton<IHostProcesses, FlatpakHostProcesses>()
            : services.AddSingleton<IHostProcesses, NativeHostProcesses>();
}
