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
        WebKitEnvironment.DisableNvidiaExplicitSync();
        DesktopIdentity.Apply();

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
        app.MainWindow.SetSize(new Size(1280, 900));
        app.MainWindow.SetIconFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", "titanite-icon.png"));
        
        app.MainWindow.SetDevToolsEnabled(app.Environment.IsDevelopment);
        app.MainWindow.SetContextMenuEnabled(app.Environment.IsDevelopment);

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            app.MainWindow.ShowMessage("Unhandled Exception", e.ExceptionObject.ToString() ?? "Unknown Exception");
        };

        app.Run();
    }
}
