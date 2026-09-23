using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Hosting;

namespace Titanite.UI;

public partial class App : ComponentBase
{
    [Inject]
    private IAppStartupService Startup { get; set; } = null!;

    protected override void OnInitialized() => _ = Startup.RunAsync();
}
