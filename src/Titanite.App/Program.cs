using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Settings;
using Titanite.App.Desktop;
using Titanite.App.Hosting;
using Titanite.Bootstrap;
using Titanite.UI.Services;
using System.Drawing;

namespace Titanite.App;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        WebKitEnvironment.EnsureOsIsLinux();
        WebKitEnvironment.DisableNvidiaExplicitSync();
        DesktopIdentity.Apply();

        var appBuilder = PhotinoBlazorApp.CreateBuilder();

        appBuilder.Services
            .AddLogging(logging => logging.AddConsole())
            .AddTitanite()
            .AddTitaniteUI()
            .AddAppEnvironment();

        appBuilder.RootComponents.Add<UI.App>("app");

        var app = appBuilder
            .Build()
            .RegisterCustomSchemes();

        var applicationInfo  = app.Services.GetRequiredService<IApplicationInfo>();
        var settings = app.Services.GetRequiredService<IAppSettingsService>().GetAsync().GetAwaiter().GetResult();
        
        app.MainWindow.SetDevToolsEnabled(applicationInfo.IsDevelopment);
        app.MainWindow.SetContextMenuEnabled(applicationInfo.IsDevelopment);
        app.MainWindow.SetTitle("Titanite");
        app.MainWindow.SetSize(new Size(1280, 900));
        app.MainWindow.SetZoom(settings.InterfaceScale);
        app.MainWindow.SetIconFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", "titanite-icon.png"));
        app.MainWindow.RegisterCreatedHandler((_, _) => WindowMinimumSize.Apply(app.MainWindow, settings.InterfaceScale));

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            app.MainWindow.ShowMessage("Unhandled Exception", e.ExceptionObject.ToString() ?? "Unknown Exception");
        };

        app.Run();
    }
}
