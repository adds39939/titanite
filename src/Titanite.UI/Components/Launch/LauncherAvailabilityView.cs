using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;

namespace Titanite.UI.Components.Launch;

public abstract class LauncherAvailabilityView : ComponentBase, IDisposable
{
    [Inject]
    private IGameLauncherAvailabilityWatcher Watcher { get; set; } = null!;

    protected LauncherAvailability Availability { get; private set; } = LauncherAvailability.Unknown;

    protected bool CanSave => Availability.IsAvailable;

    protected string? UnavailableReason => Availability.Explanation;

    protected override void OnInitialized()
    {
        Availability = Watcher.Current;
        Watcher.Changed += OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged(LauncherAvailability availability) =>
        _ = InvokeAsync(() =>
        {
            Availability = availability;

            StateHasChanged();
        });

    public virtual void Dispose() => Watcher.Changed -= OnAvailabilityChanged;
}
