using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;

namespace Titanite.UI.Components.GameConfig;

public partial class CopyConfigDialog : ComponentBase
{
    [Inject]
    private IGameLibrary Library { get; set; } = null!;

    [Inject]
    private ILaunchOptionsStore LaunchOptionsStore { get; set; } = null!;

    [Inject]
    private IPresetService Presets { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public required GameEntry Entry { get; set; }

    [Parameter]
    public EventCallback<GameEntry> OnChoose { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    internal sealed record Candidate(GameEntry Entry, string Summary)
    {
        public string? PresetName { get; init; }
    }

    private IReadOnlyList<Candidate> Candidates { get; set; } = [];

    private string SearchTerm { get; set; } = string.Empty;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private IReadOnlyList<Candidate> Matches => [.. Candidates.Where(MatchesSearch)];

    private string EmptyMessage => Candidates.Count == 0
        ? "No other game has launch options or a preset to copy."
        : "No game matches that search.";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            List<GameEntry> games =
            [
                .. (await Library.GetGamesAsync()).Where(game => game.Id != Entry.Id && !game.IsTool)
            ];

            var options = await LaunchOptionsStore.GetManyAsync([.. games.Select(game => game.Id)]);

            var applied = await Presets.GetAssignmentsAsync();

            var names = (await Presets.GetAllAsync())
                .ToDictionary(preset => preset.Id, preset => preset.Name, StringComparer.OrdinalIgnoreCase);

            Candidates =
            [
                .. games
                    .Select(game => new Candidate(game, Describe(options.GetValueOrDefault(game.Id) ?? new()))
                    {
                        PresetName = applied.TryGetValue(game.Id, out var id) ? names.GetValueOrDefault(id) : null
                    })
                    .Where(candidate =>
                        candidate.PresetName is not null ||
                        options.GetValueOrDefault(candidate.Entry.Id) is { IsEmpty: false })
            ];
        }
        catch (Exception e)
        {
            Candidates = [];
            LoadError = $"Could not read the library: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal static string Describe(LaunchOptions options)
    {
        var parts = new List<string>();

        if (options.Environment.Count > 0)
        {
            parts.Add($"{options.Environment.Count} {Pluralise(options.Environment.Count, "variable", "variables")}");
        }

        if (options.Wrapper.Count > 0)
        {
            parts.Add("a launch chain");
        }

        if (options.Arguments.Count > 0)
        {
            parts.Add($"{options.Arguments.Count} {Pluralise(options.Arguments.Count, "argument", "arguments")}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Nothing set";
    }

    private static string Pluralise(int count, string singular, string plural) =>
        count == 1 ? singular : plural;

    private bool MatchesSearch(Candidate candidate)
    {
        var term = SearchTerm.Trim();

        return term.Length == 0 ||
               candidate.Entry.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
               candidate.Entry.Id.Id.Contains(term, StringComparison.Ordinal) ||
               (candidate.PresetName?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    private void OnSearchInput(ChangeEventArgs args) =>
        SearchTerm = args.Value?.ToString() ?? string.Empty;

    private Task Choose(Candidate candidate) => OnChoose.InvokeAsync(candidate.Entry);

    private Task Cancel() => OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Cancel() : Task.CompletedTask;
}
