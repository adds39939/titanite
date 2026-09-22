namespace Titanite.Abstractions.Launchers;

public interface IGameLauncherAvailabilityWatcher
{
    LauncherAvailability Current { get; }

    event Action<LauncherAvailability> Changed;
}
