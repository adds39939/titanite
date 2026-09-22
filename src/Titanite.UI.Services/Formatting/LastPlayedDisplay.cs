namespace Titanite.UI.Services.Formatting;

public static class LastPlayedDisplay
{
    private const int DaysBeforeShowingADate = 30;

    public static string Format(DateTimeOffset? lastPlayed)
    {
        if (lastPlayed is null)
        {
            return "Never";
        }

        var elapsed = DateTimeOffset.Now - lastPlayed.Value;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "Just now",
            { TotalHours: < 1 } => Count(elapsed.Minutes, "minute"),
            { TotalDays: < 1 } => Count(elapsed.Hours, "hour"),
            { TotalDays: < DaysBeforeShowingADate } => Count(elapsed.Days, "day"),
            _ => lastPlayed.Value.ToLocalTime().ToString("d MMM yyyy")
        };
    }

    private static string Count(int value, string unit) =>
        value == 1 ? $"1 {unit} ago" : $"{value} {unit}s ago";
}
