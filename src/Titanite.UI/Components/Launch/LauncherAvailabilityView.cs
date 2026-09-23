using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Launchers;

namespace Titanite.UI.Components.Launch;

public abstract class LauncherAvailabilityView : ComponentBase, IDisposable
{
    [Inject]
    private IGameLauncherAvailabilityWatcher Watcher { get; set; } = null!;

    [Inject]
    private IAppStartupService Startup { get; set; } = null!;

    protected LauncherAvailability Availability { get; private set; } = LauncherAvailability.Unknown;

    protected bool CanSave => !Startup.IsRunning && Availability.IsAvailable;

    protected string? UnavailableReason => Startup.IsRunning
        ? $"{Startup.Activity ?? "Starting up…"} Saving is available once that is done."
        : Availability.Explanation;

    protected override void OnInitialized()
    {
        Availability = Watcher.Current;
        Watcher.Changed += OnAvailabilityChanged;
        Startup.Changed += OnStartupChanged;
    }

    private void OnAvailabilityChanged(LauncherAvailability availability) =>
        _ = InvokeAsync(() =>
        {
            Availability = availability;

            StateHasChanged();
        });

    private void OnStartupChanged() => _ = InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Watcher.Changed -= OnAvailabilityChanged;
        Startup.Changed -= OnStartupChanged;
    }
}
