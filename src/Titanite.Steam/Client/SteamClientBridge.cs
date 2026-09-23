using Microsoft.Extensions.Logging;
using Titanite.Steam.Vdf;
using System.Net.WebSockets;

namespace Titanite.Steam.Client;

internal sealed class SteamClientBridge(ILogger<SteamClientBridge> logger) : ISteamClientBridge, IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly HttpClient Http = new() { Timeout = ConnectTimeout };

    private readonly SemaphoreSlim _turns = new(1, 1);
    private readonly SemaphoreSlim _connecting = new(1, 1);

    private SteamClientSession? _session;

    public async Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_session is { IsOpen: true } open)
        {
            return open;
        }

        await _connecting.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_session is { IsOpen: true } opened)
            {
                return opened;
            }

            if (_session is { } closed)
            {
                _session = null;

                await closed.CloseAsync().ConfigureAwait(false);
            }

            if (await OpenAsync(cancellationToken).ConfigureAwait(false) is not { } socket)
            {
                return null;
            }

            logger.LogDebug("Connected to Steam's interface.");

            return _session = new SteamClientSession(socket, _turns, logger);
        }
        finally
        {
            _connecting.Release();
        }
    }

    public void Dispose()
    {
        _session?.Abort();
        _session = null;
    }

    private async Task<ClientWebSocket?> OpenAsync(CancellationToken cancellationToken)
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

            return socket;
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
