using Titanite.Core.Games;
using Titanite.Core.Settings;

namespace Titanite.UI.Services.Library;

public static class LibraryFilters
{
    public static IReadOnlyList<LibraryFilter> All { get; } = Enum.GetValues<LibraryFilter>();

    public static string Title(this LibraryFilter filter) => filter switch
    {
        LibraryFilter.NativeGames => "Show native games",
        LibraryFilter.Tools => "Show tools",
        _ => filter.ToString()
    };

    public static bool IsOnIn(this LibraryFilter filter, AppSettings settings) => filter switch
    {
        LibraryFilter.NativeGames => settings.ShowNativeGames,
        LibraryFilter.Tools => settings.ShowTools,
        _ => false
    };

    public static AppSettings With(this LibraryFilter filter, AppSettings settings, bool isOn) => filter switch
    {
        LibraryFilter.NativeGames => settings with { ShowNativeGames = isOn },
        LibraryFilter.Tools => settings with { ShowTools = isOn },
        _ => settings
    };

    public static bool Admits(GameEntry game, bool showNativeGames, bool showTools) =>
        game.IsTool ? showTools : !game.RunsNatively || showNativeGames;
}
