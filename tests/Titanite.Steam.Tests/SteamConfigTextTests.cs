using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public class SteamConfigTextTests
{
    private const string Document =
        "\"UserLocalConfigStore\"\n" +
        "{\n" +
        "\t\"Software\"\n" +
        "\t{\n" +
        "\t\t\"Valve\"\n" +
        "\t\t{\n" +
        "\t\t\t\"Steam\"\n" +
        "\t\t\t{\n" +
        "\t\t\t\t\"CachedPrefs\"\t\t\"{\\\"a\\\":1,\\\"b\\\":\\\"two\\\"}\"\n" +
        "\t\t\t\t\"apps\"\n" +
        "\t\t\t\t{\n" +
        "\t\t\t\t\t\"2357570\"\n" +
        "\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LastPlayed\"\t\t\"1786305709\"\n" +
        "\t\t\t\t\t\t\"LaunchOptions\"\t\t\"PROTON_ENABLE_HDR=1 %command%\"\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t\t\"440\"\n" +
        "\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LastPlayed\"\t\t\"1700000000\"\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t}\n" +
        "\t\t\t}\n" +
        "\t\t}\n" +
        "\t}\n" +
        "}\n";

    private static string[] PathTo(string appId) =>
        ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId, "LaunchOptions"];

    [Fact]
    public void ReadsAValueByPath() =>
        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", SteamConfigText.GetValue(Document, PathTo("2357570")));

    [Fact]
    public void ReturnsNullForAKeyThatIsNotSet() =>
        Assert.Null(SteamConfigText.GetValue(Document, PathTo("440")));

    [Fact]
    public void ReturnsNullForAnAppThatIsNotPresent() =>
        Assert.Null(SteamConfigText.GetValue(Document, PathTo("999999")));

    [Fact]
    public void DoesNotConfuseAppsWithTheSameKeyName()
    {
        var path = new[] { "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", "440", "LastPlayed" };

        Assert.Equal("1700000000", SteamConfigText.GetValue(Document, path));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has \"quotes\"", "has \\\"quotes\\\"")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("tab\there", "tab\\there")]
    public void ResolvesTheEscapesSteamWrites(string value, string escaped) =>
        Assert.Equal(value, SteamConfigText.Unescape(escaped));
}
