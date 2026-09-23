using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Hosting;

namespace Titanite.UI.Components.Startup;

public partial class StartupStatus : ComponentBase, IDisposable
{
    [Inject]
    private IAppStartupService Startup { get; set; } = null!;

    protected override void OnInitialized() => Startup.Changed += OnStartupChanged;

    private void OnStartupChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Startup.Changed -= OnStartupChanged;
}
