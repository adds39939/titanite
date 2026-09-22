using Titanite.UI.Services.Formatting;

namespace Titanite.UI.Services.Tests.Formatting;

public class LastPlayedDisplayTests
{
    private static string Ago(TimeSpan elapsed) => LastPlayedDisplay.Format(DateTimeOffset.Now - elapsed);

    [Fact]
    public void SaysNeverWhenAGameHasNotBeenPlayed() =>
        Assert.Equal("Never", LastPlayedDisplay.Format(null));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(59)]
    public void SaysJustNowWithinTheFirstMinute(int seconds) =>
        Assert.Equal("Just now", Ago(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(1, "1 minute ago")]
    [InlineData(2, "2 minutes ago")]
    [InlineData(59, "59 minutes ago")]
    public void CountsMinutesWithinTheHour(int minutes, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(60, "1 hour ago")]
    [InlineData(61, "1 hour ago")]
    [InlineData(119, "1 hour ago")]
    [InlineData(120, "2 hours ago")]
    public void CountsHoursOncePastFiftyNineMinutes(int minutes, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(23, "23 hours ago")]
    [InlineData(24, "1 day ago")]
    [InlineData(47, "1 day ago")]
    [InlineData(48, "2 days ago")]
    public void CountsDaysOncePastTwentyFourHours(int hours, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromHours(hours)));

    [Fact]
    public void ShowsADateOnceTheCountStopsHelping()
    {
        var played = DateTimeOffset.Now - TimeSpan.FromDays(45);

        Assert.Equal(played.ToLocalTime().ToString("d MMM yyyy"), LastPlayedDisplay.Format(played));
    }

    [Fact]
    public void KeepsCountingDaysRightUpToThatPoint() =>
        Assert.Equal("29 days ago", Ago(TimeSpan.FromDays(29)));

    [Fact]
    public void TreatsAFutureTimestampAsNow() =>
        Assert.Equal("Just now", LastPlayedDisplay.Format(DateTimeOffset.Now.AddHours(2)));
}
