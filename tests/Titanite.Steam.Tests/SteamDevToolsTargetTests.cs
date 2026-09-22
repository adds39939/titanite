using Titanite.Steam.Client;

namespace Titanite.Steam.Tests;

public sealed class SteamDevToolsTargetTests
{
    private static string Listing(params string[] pages) => $"[{string.Join(",", pages)}]";

    private static string Page(string title, string url, string? socket = "ws://127.0.0.1:8080/devtools/page/1") =>
        socket is null
            ? $$"""{ "title": {{Quote(title)}}, "url": {{Quote(url)}} }"""
            : $$"""
              { "title": {{Quote(title)}}, "url": {{Quote(url)}}, "webSocketDebuggerUrl": {{Quote(socket)}} }
              """;

    private static string Quote(string value) => $"\"{value}\"";

    private const string InterfaceUrl = "https://steamloopback.host/index.html?LANGUAGE=english";

    [Fact]
    public void FindsThePageSteamRunsItsOwnCodeIn()
    {
        var listing = Listing(
            Page("Steam Root Menu", "about:blank?createflags=4538378"),
            Page("Welcome to Steam", "https://store.steampowered.com/"),
            Page("SharedJSContext", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/ABC"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/ABC", SteamDevToolsTarget.FindSharedContext(listing));
    }

    [Fact]
    public void WillNotSettleForAPageThatMerelyBelongsToSteam()
    {
        var listing = Listing(
            Page("Welcome to Steam", "https://store.steampowered.com/"),
            Page("Steam Community", "https://steamcommunity.com/"));

        Assert.Null(SteamDevToolsTarget.FindSharedContext(listing));
    }

    [Fact]
    public void FindsThePageOnAVersionThatNamesItSomethingElse()
    {
        var listing = Listing(
            Page("Steam Shared Context presented by Valve", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/XYZ"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/XYZ", SteamDevToolsTarget.FindSharedContext(listing));
    }

    [Fact]
    public void PrefersTheNamedPageWhereSeveralOfSteamsOwnAreListed()
    {
        var listing = Listing(
            Page("Steam", "https://steamloopback.host/routes/library", "ws://127.0.0.1:8080/devtools/page/FIRST"),
            Page("SharedJSContext", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/SHARED"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/SHARED", SteamDevToolsTarget.FindSharedContext(listing));
    }

    [Fact]
    public void PassesOverAPageThatCannotBeConnectedTo()
    {
        var listing = Listing(Page("SharedJSContext", InterfaceUrl, socket: null));

        Assert.Null(SteamDevToolsTarget.FindSharedContext(listing));
    }

    [Fact]
    public void ReportsNothingWhenSteamIsBetweenInterfaces()
    {
        Assert.Null(SteamDevToolsTarget.FindSharedContext("[]"));
    }
}
