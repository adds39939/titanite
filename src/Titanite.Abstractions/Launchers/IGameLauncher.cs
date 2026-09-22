using Titanite.Core.Games;
using Titanite.Core.Proton;

namespace Titanite.Abstractions.Launchers;

public interface IGameLauncher
{
    string Key { get; }

    string Name { get; }

    LauncherCapabilities Capabilities { get; }

    string? InstallLocation { get; }

    bool Launch(GameId id);

    Task<CompatibilityToolAssignments> GetCompatibilityToolAssignmentsAsync(
        CancellationToken cancellationToken = default);
}
