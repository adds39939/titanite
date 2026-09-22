using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Core.Proton;
using Titanite.Steam.Library;
using Titanite.Steam.Proton;

namespace Titanite.Steam.Client;

internal sealed class SteamLauncher(
    ISteamClient client,
    ISteamInstallLocator installLocator,
    IProtonToolService protonTools) : IGameLauncher
{
    public string Key => SteamIds.Launcher;

    public string Name => "Steam";

    public LauncherCapabilities Capabilities { get; } = new()
    {
        CanLaunchGames = true,
        CanSaveLaunchOptions = true,
        CanAssignCompatibilityTools = true,
        ReportsInstallLocation = true
    };

    public string? InstallLocation => installLocator.Locate();

    public bool Launch(GameId id) =>
        SteamIds.TryAppId(id, out var appId) && client.LaunchGame(appId);

    public Task<CompatibilityToolAssignments> GetCompatibilityToolAssignmentsAsync(
        CancellationToken cancellationToken = default) =>
        protonTools.GetAssignmentsAsync(cancellationToken);
}
