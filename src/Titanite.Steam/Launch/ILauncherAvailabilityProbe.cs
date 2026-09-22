using Titanite.Abstractions.Launchers;

namespace Titanite.Steam.Launch;

internal interface ILauncherAvailabilityProbe
{
    Task<LauncherAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);
}
