using Titanite.Core.Games;

namespace Titanite.Steam.Artwork;

internal static class ArtworkScheme
{
    public const string Name = "artwork";

    private const string Authority = "steam";

    public static string UrlFor(uint appId, GameArtworkKind kind) =>
        $"{Name}://{Authority}/{appId}/{kind.ToString().ToLowerInvariant()}";

    public static bool TryParse(string? url, out uint appId, out GameArtworkKind kind)
    {
        appId = 0;
        kind = default;

        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is [var app, var shape] &&
               uint.TryParse(app, out appId) &&
               Enum.TryParse(shape, ignoreCase: true, out kind) &&
               Enum.IsDefined(kind);
    }
}
