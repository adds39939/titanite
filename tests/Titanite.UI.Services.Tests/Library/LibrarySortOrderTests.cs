using Titanite.Core.Games;
using Titanite.Core.Settings;
using Titanite.UI.Services.Library;

namespace Titanite.UI.Services.Tests.Library;

public class LibrarySortOrderTests
{
    private static GameEntry Game(string name, DateTimeOffset? lastPlayed = null) => new()
    {
        Id = new GameId("steam", name.GetHashCode(StringComparison.Ordinal).ToString()),
        Name = name,
        InstallDirectory = $"/steam/{name}",
        LastPlayed = lastPlayed
    };

    private static DateTimeOffset MinutesAgo(int minutes) => DateTimeOffset.Now - TimeSpan.FromMinutes(minutes);

    private static string[] Order(LibrarySortOrder order, params GameEntry[] apps) =>
        [.. order.Apply(apps).Select(app => app.Name)];

    [Fact]
    public void OrdersByNameAlphabetically() =>
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            Order(LibrarySortOrder.Name, Game("Gamma"), Game("Alpha"), Game("Beta")));

    [Fact]
    public void IgnoresCaseWhenOrderingByName() =>
        Assert.Equal(
            ["apple", "Banana", "cherry"],
            Order(LibrarySortOrder.Name, Game("cherry"), Game("apple"), Game("Banana")));

    [Fact]
    public void PutsTheMostRecentlyPlayedFirst() =>
        Assert.Equal(
            ["REMATCH", "Overwatch", "Lossless Scaling"],
            Order(
                LibrarySortOrder.RecentlyPlayed,
                Game("Overwatch", MinutesAgo(240)),
                Game("Lossless Scaling", MinutesAgo(1440)),
                Game("REMATCH", MinutesAgo(32))));

    [Fact]
    public void PutsGamesNeverPlayedLast() =>
        Assert.Equal(
            ["Played", "Ancient", "Never"],
            Order(
                LibrarySortOrder.RecentlyPlayed,
                Game("Never"),
                Game("Ancient", MinutesAgo(100_000)),
                Game("Played", MinutesAgo(5))));

    [Fact]
    public void FallsBackToTheNameWhenTwoWerePlayedAtOnce()
    {
        var moment = MinutesAgo(10);

        Assert.Equal(
            ["Alpha", "Beta"],
            Order(LibrarySortOrder.RecentlyPlayed, Game("Beta", moment), Game("Alpha", moment)));
    }

    [Fact]
    public void OrdersGamesNeverPlayedByNameAmongThemselves() =>
        Assert.Equal(
            ["Alpha", "Beta"],
            Order(LibrarySortOrder.RecentlyPlayed, Game("Beta"), Game("Alpha")));

    [Theory]
    [InlineData(LibrarySortOrder.Name, "Name")]
    [InlineData(LibrarySortOrder.RecentlyPlayed, "Recently played")]
    public void NamesEachOrderForTheMenu(LibrarySortOrder order, string expected) =>
        Assert.Equal(expected, order.Title());
}
