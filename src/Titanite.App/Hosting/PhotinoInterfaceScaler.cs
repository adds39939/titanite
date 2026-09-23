using PhotinoX.App;
using Titanite.Abstractions.Hosting;

namespace Titanite.App.Hosting;

internal sealed class PhotinoInterfaceScaler : IInterfaceScaler
{
    public Task ApplyAsync(int percent)
    {
        var window = PhotinoApp.Current.MainWindow;

        window.Invoke(() => window.SetZoom(percent));

        return Task.CompletedTask;
    }
}
