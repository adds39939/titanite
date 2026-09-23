using Titanite.Bootstrap;
using Titanite.DebugServer.Components;
using Titanite.DebugServer.Hosting;
using Titanite.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddTitanite()
    .AddTitaniteUI()
    .AddAppEnvironment()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapStaticAssets();
app.UseAntiforgery();

app.MapCustomSchemes();

await app.Services.StartTitaniteAsync();

app.MapRazorComponents<Root>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Titanite.UI.App).Assembly);

app.Run();
