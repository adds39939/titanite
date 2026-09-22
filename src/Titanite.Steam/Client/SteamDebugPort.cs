using System.Net.Sockets;
using System.Net;

namespace Titanite.Steam.Client;

internal sealed class SteamDebugPort : ISteamDebugPort
{
    public const int Port = 8080;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public async Task<bool> IsListeningAsync(CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(ConnectTimeout);

        try
        {
            await client.ConnectAsync(IPAddress.Loopback, Port, attempt.Token).ConfigureAwait(false);

            return true;
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<bool> WaitUntilListeningAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            if (await IsListeningAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
