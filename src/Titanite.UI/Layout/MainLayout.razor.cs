using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Desktop;
using Titanite.Abstractions.Hosting;

namespace Titanite.UI.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private static readonly IReadOnlyList<NavItem> NavItems =
    [
        new("/", "Library", NavLinkMatch.All, "library"),
        new("/presets", "Presets"),
        new("/proton", "Proton"),
        new("/settings", "Settings")
    ];

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IApplicationInfo ApplicationInfo { get; set; } = null!;

    [Inject]
    private IBrowserService Browser { get; set; } = null!;

    private string Version => $"v{ApplicationInfo.Version}";

    private void OpenRepository() => Browser.Open(ApplicationInfo.RepositoryUrl);

    private string TabClass(NavItem item) => IsUnder(item) ? "nav-tab active" : "nav-tab";

    private bool IsUnder(NavItem item) =>
        item.Section is { } section &&
        Navigation.ToBaseRelativePath(Navigation.Uri).TrimStart('/')
            .StartsWith(section, StringComparison.OrdinalIgnoreCase);

    private sealed record NavItem(
        string Path,
        string Label,
        NavLinkMatch Match = NavLinkMatch.Prefix,
        string? Section = null);
}
