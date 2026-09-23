using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Updates;
using Titanite.UI.Services.Editing;

namespace Titanite.UI.Components.Updates;

public partial class UpdateButton : ComponentBase
{
    [Inject]
    private IAppUpdater Updater { get; set; } = null!;

    [Inject]
    private IAppLifetime Lifetime { get; set; } = null!;

    [Inject]
    private IUnsavedChanges UnsavedChanges { get; set; } = null!;

    private AppUpdate? Update { get; set; }

    private UpdateState State { get; set; }

    private int? Percent { get; set; }

    private bool IsConfirming { get; set; }

    private string Label => State switch
    {
        UpdateState.Installing when Percent is null => $"Downloading Titanite v{Update?.Version}",
        UpdateState.Installing when Percent < 100 => $"Downloading Titanite v{Update?.Version} ({Percent}%)",
        UpdateState.Installing => $"Installing Titanite v{Update?.Version}",
        UpdateState.Installed => $"Restart Titanite to use v{Update?.Version}",
        UpdateState.Failed => $"Updating to v{Update?.Version} failed. Click to try again",
        _ => $"Update Titanite to v{Update?.Version}"
    };

    private string StateClass => State switch
    {
        UpdateState.Installing => "update update-installing",
        UpdateState.Installed => "update update-installed",
        UpdateState.Failed => "update update-failed",
        _ => "update"
    };

    protected override async Task OnInitializedAsync() =>
        Update = await Updater.CheckAsync();

    private Task RequestUpdateAsync()
    {
        if (!UnsavedChanges.Any)
        {
            return InstallAsync(discardingChanges: false);
        }

        IsConfirming = true;

        return Task.CompletedTask;
    }

    private Task ConfirmAsync()
    {
        IsConfirming = false;

        return InstallAsync(discardingChanges: true);
    }

    private void CancelConfirmation() => IsConfirming = false;

    private async Task InstallAsync(bool discardingChanges)
    {
        if (Update is null || State is UpdateState.Installing or UpdateState.Installed)
        {
            return;
        }

        State = UpdateState.Installing;
        Percent = null;

        var installed = await Updater.InstallAsync(Update, new Progress<double>(ReportProgress));

        State = installed ? UpdateState.Installed : UpdateState.Failed;

        if (installed && (discardingChanges || !UnsavedChanges.Any))
        {
            Lifetime.Restart();
        }
    }

    private void ReportProgress(double fraction)
    {
        var percent = (int)Math.Clamp(Math.Floor(fraction * 100), 0, 100);

        if (State != UpdateState.Installing || percent == Percent)
        {
            return;
        }

        Percent = percent;
        _ = InvokeAsync(StateHasChanged);
    }

    private enum UpdateState
    {
        Available,

        Installing,

        Installed,

        Failed
    }
}
