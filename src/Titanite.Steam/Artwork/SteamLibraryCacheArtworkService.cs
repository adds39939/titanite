using Titanite.Abstractions.Hosting;
using Titanite.Core.Games;
using Titanite.Steam.Library;
using System.Collections.Concurrent;

namespace Titanite.Steam.Artwork;

internal sealed class SteamLibraryCacheArtworkService(ISteamInstallLocator steam)
    : IArtworkSource, ICustomSchemeHandler
{
    public string Scheme => ArtworkScheme.Name;
    
    private readonly ConcurrentDictionary<(uint AppId, GameArtworkKind Kind), string> _found = new();

    public Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FindFile(appId, kind) is null ? null : ArtworkScheme.UrlFor(appId, kind));

    public SchemeContent? Open(string? url)
    {
        if (!ArtworkScheme.TryParse(url, out var appId, out var kind))
        {
            return null;
        }

        return TryOpen(FindFile(appId, kind)) ??
               (_found.TryRemove((appId, kind), out _) ? TryOpen(FindFile(appId, kind)) : null);
    }

    private static SchemeContent? TryOpen(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return new SchemeContent(File.OpenRead(path), ContentTypeFor(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? FindFile(uint appId, GameArtworkKind kind)
    {
        if (_found.TryGetValue((appId, kind), out var remembered))
        {
            return remembered;
        }

        if (steam.Locate() is not { } root || SteamLibraryCache.Find(root, appId, kind) is not { } path)
        {
            return null;
        }

        _found[(appId, kind)] = path;

        return path;
    }

    private static string ContentTypeFor(string path) =>
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
}
