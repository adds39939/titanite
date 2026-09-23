using Microsoft.Extensions.Logging;
using Titanite.Steam.Vdf;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;

namespace Titanite.Steam.Client;

internal sealed class SteamClientSession(
    ClientWebSocket socket,
    SemaphoreSlim turns,
    ILogger logger) : ISteamClientSession
{
    private static readonly TimeSpan EvaluateTimeout = TimeSpan.FromSeconds(20);

    private const int DetailsTimeoutMilliseconds = 5000;

    private int _lastId;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        const string expression = """
            (() => {
              const app = window.App;
              const store = window.appStore;
              if (typeof app?.GetServicesInitialized !== "function" || !store || !("m_bIsInitialized" in store)) {
                return { tag: "unknown" };
              }
              return { tag: app.GetServicesInitialized() && store.m_bIsInitialized ? "ready" : "starting" };
            })()
            """;

        if (await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false) is not { } answer)
        {
            return false;
        }

        switch (Tag(answer))
        {
            case "ready":
                return true;

            case "unknown":
                logger.LogWarning("Steam does not say whether it has finished starting on this version.");

                return true;

            default:
                return false;
        }
    }

    public async Task<SteamAppDetails?> GetAppDetailsAsync(
        uint appId,
        CancellationToken cancellationToken = default) =>
        (await GetAppDetailsAsync([appId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(appId);

    public async Task<IReadOnlyDictionary<uint, SteamAppDetails>> GetAppDetailsAsync(
        IReadOnlyCollection<uint> appIds,
        CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<uint, SteamAppDetails>();

        if (appIds.Count == 0)
        {
            return found;
        }

        var expression = $$"""
            (async () => {
              const apps = window.SteamClient?.Apps;
              if (!apps?.RegisterForAppDetails) return { tag: "no-api" };
              const read = appId => new Promise(resolve => {
                let registration = null;
                let settled = false;
                const release = () => {
                  if (registration && typeof registration.unregister === "function") {
                    registration.unregister();
                  }
                };
                const finish = value => {
                  if (settled) return;
                  settled = true;
                  release();
                  resolve(value);
                };
                registration = apps.RegisterForAppDetails(appId, finish);
                if (settled) release();
                setTimeout(() => finish(null), {{DetailsTimeoutMilliseconds}});
              });
              const appIds = {{JsonSerializer.Serialize(appIds)}};
              const details = await Promise.all(appIds.map(read));
              return {
                tag: "ok",
                apps: appIds.flatMap((appId, index) => details[index]
                  ? [{
                      appId,
                      launchOptions: details[index].strLaunchOptions ?? "",
                      compatToolName: details[index].strCompatToolName ?? ""
                    }]
                  : [])
              };
            })()
            """;

        if (await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false) is not { } answer)
        {
            return found;
        }

        if (Tag(answer) != "ok" || !answer.TryGetProperty("apps", out var apps))
        {
            logger.LogWarning("Steam does not offer app details on this version.");

            return found;
        }

        foreach (var app in apps.EnumerateArray())
        {
            found[app.GetProperty("appId").GetUInt32()] = new SteamAppDetails(
                Text(app, "launchOptions"),
                Text(app, "compatToolName"));
        }

        foreach (var missing in appIds.Where(appId => !found.ContainsKey(appId)))
        {
            logger.LogWarning("Steam did not report any details for {AppId}.", missing);
        }

        return found;
    }

    public Task<bool> SetLaunchOptionsAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            "SetAppLaunchOptions",
            appId,
            launchOptions,
            cancellationToken);

    public Task<bool> SetCompatToolAsync(
        uint appId,
        string toolName,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            "SpecifyCompatTool",
            appId,
            toolName,
            cancellationToken);

    private async Task<bool> ApplyAsync(
        string method,
        uint appId,
        string argument,
        CancellationToken cancellationToken)
    {
        var expression = $$"""
            (async () => {
              const apps = window.SteamClient?.Apps;
              if (!apps?.{{method}}) return { tag: "no-api" };
              await apps.{{method}}({{appId}}, {{JsonSerializer.Serialize(argument)}});
              return { tag: "ok" };
            })()
            """;

        if (await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false) is not { } answer)
        {
            return false;
        }

        if (Tag(answer) == "ok")
        {
            return true;
        }

        logger.LogWarning("Steam does not offer {Method} on this version.", method);

        return false;
    }

    private async Task<JsonElement?> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _lastId);

        var request = JsonSerializer.Serialize(new
        {
            id,
            method = "Runtime.evaluate",
            @params = new
            {
                expression,
                awaitPromise = true,
                returnByValue = true
            }
        });

        await turns.WaitAsync(cancellationToken).ConfigureAwait(false);

        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(EvaluateTimeout);

        try
        {
            await socket
                .SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, attempt.Token)
                .ConfigureAwait(false);

            while (true)
            {
                using var reply = JsonDocument.Parse(await ReceiveAsync(attempt.Token).ConfigureAwait(false));

                if (!reply.RootElement.TryGetProperty("id", out var replyId) || replyId.GetInt32() != id)
                {
                    continue;
                }

                if (reply.RootElement.TryGetProperty("error", out var error))
                {
                    logger.LogWarning("Steam refused the request: {Error}.", error.ToString());

                    return null;
                }

                if (!reply.RootElement.TryGetProperty("result", out var outcome))
                {
                    return null;
                }

                if (outcome.TryGetProperty("exceptionDetails", out var thrown))
                {
                    logger.LogWarning("Steam's interface threw: {Exception}.", thrown.ToString());

                    return null;
                }

                if (!outcome.TryGetProperty("result", out var value) ||
                    !value.TryGetProperty("value", out var answer))
                {
                    return null;
                }

                return answer.Clone();
            }
        }
        catch (Exception e) when (e is WebSocketException or JsonException or ObjectDisposedException)
        {
            logger.LogWarning(e, "Lost the connection to Steam part way through.");

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Steam did not answer within {Timeout}.", EvaluateTimeout);

            return null;
        }
        finally
        {
            turns.Release();
        }
    }

    private async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (true)
        {
            var part = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (part.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("Steam closed the connection.");
            }

            message.Write(buffer, 0, part.Count);

            if (part.EndOfMessage)
            {
                return message.ToArray();
            }
        }
    }

    private static string Tag(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("tag", out var tag)
            ? tag.GetString() ?? string.Empty
            : string.Empty;

    private static string Text(JsonElement answer, string property) =>
        answer.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    public bool IsOpen => socket.State == WebSocketState.Open;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Abort()
    {
        socket.Abort();
        socket.Dispose();
    }

    public async Task CloseAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                await socket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, null, closing.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
        finally
        {
            socket.Dispose();
        }
    }
}
