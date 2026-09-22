using Titanite.Core.Games;

namespace Titanite.Steam;

internal static class SteamIds
{
    public const string Launcher = "steam";

    public static GameId For(uint appId) => new(Launcher, appId.ToString());

    public static bool TryAppId(GameId id, out uint appId)
    {
        appId = 0;

        return string.Equals(id.Launcher, Launcher, StringComparison.OrdinalIgnoreCase) &&
               uint.TryParse(id.Id, out appId);
    }

    public static uint AppId(GameId id) =>
        TryAppId(id, out var appId)
            ? appId
            : throw new ArgumentException($"{id} is not a Steam game.", nameof(id));
}
