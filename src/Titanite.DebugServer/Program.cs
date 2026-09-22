using Titanite.Abstractions.Launchers;
using Titanite.Bootstrap;
using Titanite.DebugServer.Components;
using Titanite.DebugServer;
using Titanite.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddTitanite()
    .AddTitaniteUI();

builder.Services.Decorate<IGameArtwork, HttpArtworkService>();

var app = builder.Build();

app.MapStaticAssets();
app.UseAntiforgery();

app.MapCustomSchemes();

await app.Services.StartTitaniteAsync();

app.MapRazorComponents<Root>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Titanite.UI.App).Assembly);

app.Run();
