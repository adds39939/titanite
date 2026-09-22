using Microsoft.Extensions.Logging;
using Titanite.Steam.Vdf;
using System.Net.WebSockets;

namespace Titanite.Steam.Client;

internal sealed class SteamClientBridge(ILogger<SteamClientBridge> logger) : ISteamClientBridge
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private static readonly HttpClient Http = new() { Timeout = ConnectTimeout };

    private readonly SemaphoreSlim _turns = new(1, 1);

    public async Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(ConnectTimeout);

        ClientWebSocket? socket = null;

        try
        {
            var listing = await Http
                .GetStringAsync($"http://127.0.0.1:{SteamDebugPort.Port}/json", attempt.Token)
                .ConfigureAwait(false);

            if (SteamDevToolsTarget.FindSharedContext(listing) is not { } address)
            {
                logger.LogInformation("Steam is listening but is not showing its own interface yet.");

                return null;
            }

            socket = new ClientWebSocket();

            await socket.ConnectAsync(new Uri(address), attempt.Token).ConfigureAwait(false);

            return new SteamClientSession(socket, _turns, logger);
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or UriFormatException)
        {
            logger.LogDebug(e, "Steam could not be reached.");

            socket?.Dispose();

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Steam did not answer within {Timeout}.", ConnectTimeout);

            socket?.Dispose();

            return null;
        }
        catch
        {
            socket?.Dispose();

            throw;
        }
    }
}
