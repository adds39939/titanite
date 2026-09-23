using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;

namespace Titanite.UI.Components.Game;

public partial class GameArtwork : ComponentBase
{
    [Inject]
    private IGameArtwork Artwork { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public GameId GameId { get; set; }

    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public GameArtworkKind Kind { get; set; } = GameArtworkKind.Capsule;

    private string? Source { get; set; }

    private bool HasFailed { get; set; }

    private string ShapeClass => Kind == GameArtworkKind.Capsule ? "capsule" : "header";

    private string Initials => GetInitials(Name);

    private (GameId Id, GameArtworkKind Kind)? _lookedUp;

    protected override async Task OnParametersSetAsync()
    {
        var wanted = (GameId, Kind);

        if (_lookedUp == wanted)
        {
            return;
        }

        _lookedUp = wanted;

        var source = await Artwork.GetArtworkSourceAsync(GameId, Kind);

        if (_lookedUp == wanted && source != Source)
        {
            Source = source;
            HasFailed = false;
        }
    }

    private void OnLoadFailed() => HasFailed = true;

    private static string GetInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{words[0][0]}{words[1][0]}".ToUpperInvariant()
        };
    }
}
