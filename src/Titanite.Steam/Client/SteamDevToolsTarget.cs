using System.Text.Json;

namespace Titanite.Steam.Client;

internal static class SteamDevToolsTarget
{
    private const string InterfaceHost = "steamloopback.host";

    private const string SharedContextTitle = "SharedJSContext";

    public static string? FindSharedContext(string listing)
    {
        List<(string Title, string Address)> candidates = [];

        using var document = JsonDocument.Parse(listing);

        foreach (var page in document.RootElement.EnumerateArray())
        {
            if (!page.TryGetProperty("url", out var url) ||
                url.GetString() is not { } address ||
                !address.Contains(InterfaceHost, StringComparison.Ordinal))
            {
                continue;
            }

            if (!page.TryGetProperty("webSocketDebuggerUrl", out var socket) ||
                socket.GetString() is not { } socketUrl)
            {
                continue;
            }

            var title = page.TryGetProperty("title", out var name) ? name.GetString() ?? string.Empty : string.Empty;

            candidates.Add((title, socketUrl));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate.Title, SharedContextTitle, StringComparison.Ordinal))
            {
                return candidate.Address;
            }
        }

        return candidates[0].Address;
    }
}
