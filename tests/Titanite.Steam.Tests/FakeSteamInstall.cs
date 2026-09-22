using Titanite.Steam.Library;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

internal static class FakeSteamInstall
{
    public static ISteamInstallLocator At(string? root)
    {
        var locator = A.Fake<ISteamInstallLocator>();

        A.CallTo(() => locator.Locate()).Returns(root);

        return locator;
    }
}
