using Titanite.Core.Settings;
using Titanite.Core.Games;

namespace Titanite.UI.Services.Library;

public static class LibrarySortOrders
{
    public static IOrderedEnumerable<GameEntry> Apply(
        this LibrarySortOrder order,
        IEnumerable<GameEntry> apps) => order switch
    {
        LibrarySortOrder.RecentlyPlayed => apps
            .OrderByDescending(app => app.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
    };

    public static string Title(this LibrarySortOrder order) => order switch
    {
        LibrarySortOrder.Name => "Name",
        LibrarySortOrder.RecentlyPlayed => "Recently played",
        _ => order.ToString()
    };
}
