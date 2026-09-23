using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Settings;

using System.Globalization;

namespace Titanite.DebugServer.Components;

public partial class Root : ComponentBase
{
    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    private string Zoom { get; set; } = "1";

    protected override async Task OnInitializedAsync() =>
        Zoom = ((await Settings.GetAsync()).InterfaceScale / 100m).ToString(CultureInfo.InvariantCulture);
}
