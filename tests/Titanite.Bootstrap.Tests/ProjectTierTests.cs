using Titanite.Abstractions.Launchers;

namespace Titanite.Bootstrap.Tests;

public class ProjectTierTests
{
    private static readonly string[] Adapters =
        ["Titanite.Steam", "Titanite.Catalog", "Titanite.Storage", "Titanite.Platform"];

    [Fact]
    public void UiReferencesNoAdapter()
    {
        string?[] referenced =
        [
            .. typeof(Titanite.UI.App).Assembly
                .GetReferencedAssemblies()
                .Select(name => name.Name)
        ];

        Assert.All(Adapters, adapter => Assert.DoesNotContain(adapter, referenced));
    }

    [Fact]
    public void AbstractionsNamesNoLauncher()
    {
        Type[] offenders =
        [
            .. typeof(IGameLibrary).Assembly
                .GetTypes()
                .Where(type => type.Name.Contains("Steam", StringComparison.Ordinal))
        ];

        Assert.Empty(offenders);
    }

    [Fact]
    public void UiNamesNoLauncherInItsTypes()
    {
        Type[] offenders =
        [
            .. typeof(Titanite.UI.App).Assembly
                .GetTypes()
                .Where(type => type.Name.Contains("Steam", StringComparison.Ordinal))
        ];

        Assert.Empty(offenders);
    }
}
