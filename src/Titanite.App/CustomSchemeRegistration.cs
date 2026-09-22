using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Titanite.Abstractions.Hosting;

namespace Titanite.App;

internal static class CustomSchemeRegistration
{
    public static PhotinoBlazorApp RegisterCustomSchemes(this PhotinoBlazorApp app)
    {
        foreach (var handler in app.Services.GetServices<ICustomSchemeHandler>())
        {
            app.MainWindow.RegisterCustomSchemeHandler(
                handler.Scheme,
                (_, _, url, out contentType) =>
                {
                    var content = handler.Open(url);
                    contentType = content?.ContentType;

                    return content?.Content;
                });
        }

        return app;
    }
}
