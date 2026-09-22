using Titanite.Abstractions.Hosting;

namespace Titanite.DebugServer;

internal static class CustomSchemeEndpoints
{
    public static WebApplication MapCustomSchemes(this WebApplication app)
    {
        app.MapGet($"{HttpArtworkService.Prefix}/{{scheme}}/{{**rest}}", (
            string scheme,
            string rest,
            IEnumerable<ICustomSchemeHandler> handlers) =>
        {
            foreach (var handler in handlers.Where(candidate =>
                         string.Equals(candidate.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
            {
                if (handler.Open($"{scheme}://{rest}") is { } content)
                {
                    return Results.Stream(content.Content, content.ContentType);
                }
            }

            return Results.NotFound();
        });

        return app;
    }
}
