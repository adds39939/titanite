using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.UI.Components.GameConfig;

namespace Titanite.UI.Tests.Components.GameConfig;

public sealed class CopyConfigDialogTests : BunitContext
{
    private static readonly Preset Handheld = new() { Id = "handheld", Name = "Handheld" };

    private readonly IGameLibrary _library = A.Fake<IGameLibrary>();

    private readonly ILaunchOptionsStore _launchOptions = A.Fake<ILaunchOptionsStore>();

    private readonly IPresetService _presets = A.Fake<IPresetService>();

    private readonly Dictionary<GameId, string> _stored = [];

    private readonly Dictionary<GameId, string> _applied = [];

    public CopyConfigDialogTests()
    {
        A.CallTo(() => _launchOptions.GetManyAsync(A<IReadOnlyCollection<GameId>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<GameId> ids, CancellationToken _) =>
                (IReadOnlyDictionary<GameId, LaunchOptions>)ids.ToDictionary(
                    id => id,
                    id => LaunchOptions.Parse(_stored.GetValueOrDefault(id, string.Empty))));

        A.CallTo(() => _presets.GetAllAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<Preset>>([Preset.Global, Handheld]);

        A.CallTo(() => _presets.GetAssignmentsAsync(A<CancellationToken>._))
            .ReturnsLazily(() => (IReadOnlyDictionary<GameId, string>)_applied);

        Services.AddSingleton(_library);
        Services.AddSingleton(_launchOptions);
        Services.AddSingleton(_presets);
    }

    [Fact]
    public void OffersAGameThatOnlyHasLaunchOptions()
    {
        Installed(Game(400, "Portal"));
        _stored[Id(400)] = "DXVK_HUD=fps %command%";

        var dialog = RenderDialog();

        Assert.Equal(["Portal"], dialog.FindAll(".game-name").Select(name => name.TextContent));
        Assert.Empty(dialog.FindAll(".game-preset"));
    }

    [Fact]
    public void OffersAGameOnAPresetEvenWithNoLaunchOptionsOfItsOwn()
    {
        Installed(Game(400, "Portal"));
        _applied[Id(400)] = Handheld.Id;

        var dialog = RenderDialog();

        Assert.Equal(["Portal"], dialog.FindAll(".game-name").Select(name => name.TextContent));
        Assert.Equal(["Handheld"], dialog.FindAll(".game-preset").Select(tag => tag.TextContent));
    }

    [Fact]
    public void LeavesOutAGameWithNeither()
    {
        Installed(Game(400, "Portal"));

        var dialog = RenderDialog();

        Assert.Empty(dialog.FindAll(".game-name"));
        Assert.Contains("launch options or a preset", dialog.Find(".message").TextContent);
    }

    [Fact]
    public void SaysNothingAboutAPresetThatIsNoLongerThere()
    {
        Installed(Game(400, "Portal"));
        _stored[Id(400)] = "DXVK_HUD=fps %command%";
        _applied[Id(400)] = "removed";

        var dialog = RenderDialog();

        Assert.Equal(["Portal"], dialog.FindAll(".game-name").Select(name => name.TextContent));
        Assert.Empty(dialog.FindAll(".game-preset"));
    }

    [Fact]
    public void FindsAGameByThePresetItUses()
    {
        Installed(Game(400, "Portal"), Game(620, "Portal 2"));
        _applied[Id(400)] = Handheld.Id;
        _stored[Id(620)] = "DXVK_HUD=fps %command%";

        var dialog = RenderDialog();

        dialog.Find(".search").Input("handheld");

        Assert.Equal(["Portal"], dialog.FindAll(".game-name").Select(name => name.TextContent));
    }

    [Fact]
    public void NeverOffersTheGameBeingConfigured()
    {
        Installed(Game(400, "Portal"), Target);
        _applied[Id(400)] = Handheld.Id;
        _applied[Target.Id] = Handheld.Id;

        var dialog = RenderDialog();

        Assert.Equal(["Portal"], dialog.FindAll(".game-name").Select(name => name.TextContent));
    }

    [Fact]
    public void HandsBackTheGameThatWasPicked()
    {
        Installed(Game(400, "Portal"));
        _applied[Id(400)] = Handheld.Id;

        GameEntry? chosen = null;

        var dialog = Render<CopyConfigDialog>(parameters => parameters
            .Add(component => component.Entry, Target)
            .Add(component => component.OnChoose, entry => chosen = entry));

        dialog.Find(".game").Click();

        Assert.Equal(Id(400), chosen?.Id);
    }

    private IRenderedComponent<CopyConfigDialog> RenderDialog() =>
        Render<CopyConfigDialog>(parameters => parameters.Add(component => component.Entry, Target));

    private void Installed(params GameEntry[] games) =>
        A.CallTo(() => _library.GetGamesAsync(A<CancellationToken>._))
            .Returns<IReadOnlyList<GameEntry>>(games);

    private static GameId Id(uint appId) => new("steam", appId.ToString());

    private static GameEntry Target => Game(1091500, "Cyberpunk 2077");

    private static GameEntry Game(uint appId, string name) => new()
    {
        Id = Id(appId),
        Name = name,
        InstallDirectory = $"/games/common/{name}",
        IsFullyInstalled = true
    };
}
