using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Titanite.Bootstrap;
using Titanite.UI.Services;
using System.Drawing;

namespace Titanite.App;

internal class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        WebKitEnvironment.EnsureOsIsLinux();
        WebKitEnvironment.DisableDmaBufRenderer();

        var appBuilder = PhotinoBlazorApp.CreateBuilder();

        appBuilder.Services
            .AddLogging(logging => logging.AddConsole())
            .AddTitanite()
            .AddTitaniteUI();

        appBuilder.RootComponents.Add<UI.App>("app");

        var app = appBuilder
            .Build()
            .RegisterCustomSchemes();

        await app.Services.StartTitaniteAsync();

        app.MainWindow.SetTitle("Titanite");
        app.MainWindow.Size = new Size(1280, 900);

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            app.MainWindow.ShowMessage("Unhandled Exception", e.ExceptionObject.ToString() ?? "Unknown Exception");
        };

        app.Run();
    }
}
