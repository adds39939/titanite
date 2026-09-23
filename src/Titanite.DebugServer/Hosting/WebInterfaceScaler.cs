using Microsoft.JSInterop;
using Titanite.Abstractions.Hosting;

namespace Titanite.DebugServer.Hosting;

internal sealed class WebInterfaceScaler(IJSRuntime js) : IInterfaceScaler
{
    public async Task ApplyAsync(int percent) => await js.InvokeVoidAsync("titanite.setZoom", percent);
}
