using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Steam.Client;
using Titanite.Steam.Library;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Launch;

internal sealed class SteamLaunchOptionsService(
    ISteamInstallLocator installLocator,
    ISteamClient steamClient,
    ISteamClientBridge bridge,
    ISteamDebuggingService debugging,
    ISteamDebugPort debugPort,
    ILogger<SteamLaunchOptionsService> logger) : ILaunchOptionsStore, ILauncherAvailabilityProbe
{
    private const int SettleAttempts = 5;

    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan ReadyInterval = TimeSpan.FromSeconds(1);

    private static readonly IReadOnlyDictionary<GameId, string> NoCompatTools = new Dictionary<GameId, string>();

    public async Task<LaunchOptions> GetAsync(GameId id, CancellationToken cancellationToken = default) =>
        (await GetManyAsync([id], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(id)
        ?? new LaunchOptions();

    public async Task<IReadOnlyDictionary<GameId, LaunchOptions>> GetManyAsync(
        IReadOnlyCollection<GameId> ids,
        CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<GameId, LaunchOptions>();
        var appIds = ids.Where(id => SteamIds.TryAppId(id, out _)).Select(SteamIds.AppId).ToList();

        if (appIds.Count == 0)
        {
            return found;
        }

        var outstanding = new List<uint>();

        await using (var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var appId in appIds)
            {
                if (session is not null &&
                    await session.GetAppDetailsAsync(appId, cancellationToken).ConfigureAwait(false) is { } details)
                {
                    found[SteamIds.For(appId)] = LaunchOptions.Parse(details.LaunchOptions);
                }
                else
                {
                    outstanding.Add(appId);
                }
            }
        }

        if (outstanding.Count == 0)
        {
            return found;
        }

        var document = await ReadUserConfigAsync(cancellationToken).ConfigureAwait(false);

        foreach (var appId in outstanding)
        {
            found[SteamIds.For(appId)] = document is null
                ? new LaunchOptions()
                : LaunchOptions.Parse(SteamConfigText.GetValue(document, PathTo(appId)));
        }

        return found;
    }

    private async Task<string?> ReadUserConfigAsync(CancellationToken cancellationToken)
    {
        if (FindUserConfig() is not { } configPath)
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {ConfigPath}.", configPath);

            return null;
        }
    }

    public async Task<LauncherAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (!steamClient.IsRunning())
        {
            return new LauncherAvailability(
                AvailabilityStatus.Available,
                "Steam is not running — saving will start Steam and apply the changes.");
        }

        await using var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false);

        return session is null
            ? new LauncherAvailability(
                AvailabilityStatus.Blocked,
                "Steam is running but is not answering. It reads the request to offer its debugging " +
                "interface only as it starts, so restarting Steam should settle it.")
            : new LauncherAvailability(AvailabilityStatus.Available, null);
    }

    public Task<LaunchOptionsSaveResult> SaveAsync(
        GameId id,
        LaunchOptions options,
        CancellationToken cancellationToken = default) =>
        SaveManyAsync(new Dictionary<GameId, LaunchOptions> { [id] = options }, cancellationToken);

    public Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<GameId, LaunchOptions> optionsByGame,
        CancellationToken cancellationToken = default) =>
        SaveManyAsync(optionsByGame, NoCompatTools, cancellationToken);

    public async Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<GameId, LaunchOptions> optionsByGame,
        IReadOnlyDictionary<GameId, string> compatibilityToolsByGame,
        CancellationToken cancellationToken = default)
    {
        var launchOptionsByApp = optionsByGame
            .Where(pair => SteamIds.TryAppId(pair.Key, out _))
            .ToDictionary(pair => SteamIds.AppId(pair.Key), pair => pair.Value.Format());

        var compatToolsByApp = compatibilityToolsByGame
            .Where(pair => SteamIds.TryAppId(pair.Key, out _))
            .ToDictionary(pair => SteamIds.AppId(pair.Key), pair => pair.Value);

        if (launchOptionsByApp.Count == 0 && compatToolsByApp.Count == 0)
        {
            return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
        }

        var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false);

        if (session is null && !steamClient.IsRunning())
        {
            var probe = launchOptionsByApp.Keys.Concat(compatToolsByApp.Keys).First();

            var started = await StartSteamAsync(probe, cancellationToken).ConfigureAwait(false);

            if (started.Session is null)
            {
                return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.LauncherUnavailable, started.Failure);
            }

            session = started.Session;
        }

        await using var connected = session;

        if (connected is null)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.LauncherUnavailable,
                "Steam is running but is not answering. Nothing was changed.");
        }

        return await SaveThroughSteamAsync(
            connected,
            launchOptionsByApp,
            compatToolsByApp,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<StartedSteam> StartSteamAsync(uint probeAppId, CancellationToken cancellationToken)
    {
        await debugging.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        if (!steamClient.Start())
        {
            return new StartedSteam(null, "Steam could not be started. Nothing was changed.");
        }

        if (!await debugPort.WaitUntilListeningAsync(StartTimeout, cancellationToken).ConfigureAwait(false))
        {
            return new StartedSteam(null, "Steam was started but never answered. Nothing was changed.");
        }

        var deadline = DateTimeOffset.UtcNow + StartTimeout;

        while (true)
        {
            if (await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false) is { } session)
            {
                if (await session.IsReadyAsync(cancellationToken).ConfigureAwait(false) &&
                    await session.GetAppDetailsAsync(probeAppId, cancellationToken).ConfigureAwait(false) is not null)
                {
                    logger.LogInformation("Started Steam to save, and it is ready.");

                    return new StartedSteam(session, null);
                }

                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return new StartedSteam(
                    null,
                    "Steam was started but did not finish signing in. Nothing was changed.");
            }

            await Task.Delay(ReadyInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record StartedSteam(ISteamClientSession? Session, string? Failure);

    private async Task<LaunchOptionsSaveResult> SaveThroughSteamAsync(
        ISteamClientSession session,
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
        var refused = new List<string>();

        foreach (var (appId, launchOptions) in launchOptionsByApp)
        {
            if (!await session.SetLaunchOptionsAsync(appId, launchOptions, cancellationToken).ConfigureAwait(false))
            {
                refused.Add($"the launch options of {appId}");
            }
        }

        foreach (var (appId, toolName) in compatToolsByApp)
        {
            if (!await session.SetCompatToolAsync(appId, toolName, cancellationToken).ConfigureAwait(false))
            {
                refused.Add($"the Proton build of {appId}");
            }
        }

        if (refused.Count > 0)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.WriteFailed,
                $"Steam did not accept {string.Join(", ", refused)}. Anything not named here was applied.");
        }

        var mismatched = await FindLiveMismatchesAsync(
            session,
            launchOptionsByApp,
            compatToolsByApp,
            cancellationToken).ConfigureAwait(false);

        if (mismatched.Count > 0)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.WriteFailed,
                $"Steam took the change but reports something else for {string.Join(", ", mismatched)}.");
        }

        logger.LogInformation(
            "Set launch options for {AppCount} apps and Proton builds for {ToolCount} through the running client.",
            launchOptionsByApp.Count,
            compatToolsByApp.Count);

        return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
    }

    private static async Task<IReadOnlyList<string>> FindLiveMismatchesAsync(
        ISteamClientSession session,
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
        var outstanding = new List<Expectation>();

        foreach (var (appId, launchOptions) in launchOptionsByApp)
        {
            outstanding.Add(new Expectation(
                appId,
                $"the launch options of {appId}",
                details => string.Equals(details.LaunchOptions, launchOptions, StringComparison.Ordinal)));
        }

        foreach (var (appId, toolName) in compatToolsByApp)
        {
            if (toolName.Length == 0)
            {
                continue;
            }

            outstanding.Add(new Expectation(
                appId,
                $"the Proton build of {appId}",
                details => string.Equals(details.CompatToolName, toolName, StringComparison.OrdinalIgnoreCase)));
        }

        for (var attempt = 1; outstanding.Count > 0 && attempt <= SettleAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(SettleInterval, cancellationToken).ConfigureAwait(false);
            }

            var readBack = new Dictionary<uint, SteamAppDetails?>();

            foreach (var appId in outstanding.Select(expectation => expectation.AppId).Distinct())
            {
                readBack[appId] = await session.GetAppDetailsAsync(appId, cancellationToken).ConfigureAwait(false);
            }

            outstanding.RemoveAll(expectation =>
                readBack[expectation.AppId] is { } details && expectation.Matches(details));
        }

        return outstanding.Select(expectation => expectation.Description).ToList();
    }

    private sealed record Expectation(uint AppId, string Description, Func<SteamAppDetails, bool> Matches);

    private static string[] PathTo(uint appId) =>
        ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId.ToString(), "LaunchOptions"];

    private string? FindUserConfig()
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found on this machine.");

            return null;
        }

        var userdata = Path.Combine(steamRoot, "userdata");

        if (!Directory.Exists(userdata))
        {
            logger.LogWarning("Steam at {SteamRoot} has no userdata directory.", steamRoot);

            return null;
        }

        try
        {
            var configs = Directory
                .EnumerateDirectories(userdata)
                .Select(account => Path.Combine(account, "config", "localconfig.vdf"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (configs.Count == 0)
            {
                logger.LogWarning("No Steam user configuration was found under {UserDataPath}.", userdata);

                return null;
            }

            if (configs.Count > 1)
            {
                logger.LogInformation(
                    "{AccountCount} Steam accounts found; using the most recently active configuration {ConfigPath}.",
                    configs.Count,
                    configs[0]);
            }

            return configs[0];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not search {UserDataPath} for a Steam user configuration.", userdata);

            return null;
        }
    }
}
