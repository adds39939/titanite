using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;

namespace Titanite.DebugServer;

public sealed class HttpArtworkService(IGameArtwork inner) : IGameArtwork
{
    public const string Prefix = "/scheme";

    public async Task<string?> GetArtworkSourceAsync(
        GameId id,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        var source = await inner.GetArtworkSourceAsync(id, kind, cancellationToken).ConfigureAwait(false);

        return source is null || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.IsFile
            ? source
            : Rewrite(uri, source);
    }

    internal static string Rewrite(Uri uri, string source) =>
        uri.Scheme is "http" or "https" or "data"
            ? source
            : $"{Prefix}/{uri.Scheme}/{uri.Host}{uri.AbsolutePath}";
}
