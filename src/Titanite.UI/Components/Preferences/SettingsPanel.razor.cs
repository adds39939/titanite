using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;

namespace Titanite.UI.Components.Preferences;

public partial class SettingsPanel : ComponentBase
{
    [Inject]
    private IGameLauncher Launcher { get; set; } = null!;

    private string LauncherName => Launcher.Name;

    private string? InstallLocation =>
        Launcher.Capabilities.ReportsInstallLocation ? Launcher.InstallLocation : null;
}
