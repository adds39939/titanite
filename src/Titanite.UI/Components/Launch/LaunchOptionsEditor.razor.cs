using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Settings;
using Titanite.Core.Launch;
using Titanite.Core.Proton;
using Titanite.Core.Games;

namespace Titanite.UI.Components.Launch;

public partial class LaunchOptionsEditor : ComponentBase
{
    private const string ProtonSection = "Proton";
    private const string CustomSection = "Custom variables";
    private const string AppliedSection = "Applied";
    private const string RawSection = "Raw";

    private const string MangoHudCommand = "mangohud";

    private const string GameModeCommand = "gamemoderun";

    [Inject]
    private SettingCatalog Catalog { get; set; } = null!;

    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    [Parameter]
    public EventCallback<LaunchOptions> OptionsChanged { get; set; }

    [Parameter]
    public GameEntry? Entry { get; set; }

    [Parameter]
    public bool ShowProton { get; set; }

    [Parameter]
    public string CompatTool { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> CompatToolChanged { get; set; }

    [Parameter]
    public ProtonBuild? Build { get; set; }

    private ProtonCapabilities Capabilities => Build?.Capabilities ?? ProtonCapabilities.Unknown;

    private string? BuildName => Build?.DisplayName;

    private bool IsIgnored(SettingDefinition definition) => Capabilities.Reads(definition.Variable) switch
    {
        true => false,
        false => true,
        null => !definition.AppliesTo(Build)
    };

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public RenderFragment? Leading { get; set; }

    private SettingCategory? SelectedCategory { get; set; }

    private string? SelectedSpecial { get; set; }

    private string SelectedSection => SelectedCategory?.Id ?? SelectedSpecial ?? RawSection;

    private bool ShowDescriptions { get; set; }

    private string RawDraft { get; set; } = string.Empty;

    private string _lastRendered = string.Empty;

    private string NewVariableName { get; set; } = string.Empty;

    private string NewVariableValue { get; set; } = string.Empty;

    private string SearchText { get; set; } = string.Empty;

    private SettingSearch _search = SettingSearch.None;

    private bool IsSearching => _search.IsActive;

    private IReadOnlyList<string> Warnings => LaunchOptionsValidator.Validate(Options);

    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => Catalog.Find(variable.Name) is null).ToList();

    private IReadOnlyList<EnvironmentVariable> ListedCustomVariables =>
        CustomVariables.Where(variable => _search.MatchesVariable(variable.Name)).ToList();

    private IReadOnlyList<SettingGroup> AppliedGroups => Catalog.Categories
        .Select(category => new SettingGroup(
            category.Title,
            DefinitionsIn(category).Where(IsSet).Where(_search.Matches).ToList()))
        .Where(group => group.Settings.Count > 0)
        .ToList();

    private int AppliedCount => Options.Environment.Count;

    private bool AppliedHasSearchHit => AppliedGroups.Count > 0 || ListedCustomVariables.Count > 0;

    private bool IsSet(SettingDefinition definition) =>
        Options.FindEnvironment(definition.Variable) is not null;

    protected override async Task OnInitializedAsync()
    {
        SelectedCategory = VisibleCategories.FirstOrDefault();

        ShowDescriptions = (await Settings.GetAsync()).ShowVariableDescriptions;
    }

    protected override void OnParametersSet()
    {
        var formatted = Options.Format();

        if (!string.Equals(formatted, _lastRendered, StringComparison.Ordinal))
        {
            RawDraft = formatted;
            _lastRendered = formatted;
        }

        if (SelectedCategory is { } selected && !HasAnythingToShow(selected))
        {
            SelectedCategory = VisibleCategories.FirstOrDefault();
        }
    }

    private IReadOnlyList<SettingCategory> VisibleCategories =>
        Catalog.Categories.Where(HasAnythingToShow).ToList();

    private bool HasAnythingToShow(SettingCategory category)
    {
        if (category.Is(SettingCategoryIds.Cpu) || category.Is(SettingCategoryIds.MangoHud))
        {
            return true;
        }

        if (category.Command is not null)
        {
            return true;
        }

        return DefinitionsIn(category).Any(IsVisible);
    }

    private IReadOnlyList<SettingDefinition> DefinitionsIn(SettingCategory category) =>
        Catalog.In(category);

    private IEnumerable<SettingDefinition> ListedSettingsIn(SettingCategory category) =>
        DefinitionsIn(category).Where(IsVisible).Where(_search.Matches);

    private IReadOnlyList<SettingGroup> ListedGroupsIn(SettingCategory category) =>
        SettingCatalog.Group(ListedSettingsIn(category));

    private bool IsVisible(SettingDefinition definition) =>
        Options.FindEnvironment(definition.Variable) is not null ||
        (!definition.HideUnlessSet &&
            (!definition.RestrictToProtonBuild || definition.AppliesTo(Build)));

    private int SetCountIn(SettingCategory category) =>
        DefinitionsIn(category)
            .Where(IsVisible)
            .Count(definition => Options.FindEnvironment(definition.Variable) is not null) +
        (category.Command is { } command ? command.AllFlags.Count(flag => Options.HasFlag(command, flag)) : 0);

    private static string? UnheadedGroupName(SettingCategory category) =>
        category.Command is null ? null : "Settings";

    private int SetCountIn(SettingGroup group) =>
        group.Settings.Count(definition => Options.FindEnvironment(definition.Variable) is not null);

    private bool HasSearchHit(SettingCategory category) =>
        ListedSettingsIn(category).Any() ||
        (category.Command is { } command && _search.MatchesAnythingIn(command));

    private bool CustomHasSearchHit => ListedCustomVariables.Count > 0;

    private bool SelectedIsEmptyUnderSearch =>
        IsSearching && SelectedCategory is { } selected && !HasSearchHit(selected);

    private bool ShowsCommand(SettingCategory category) =>
        category.Command is { } command && _search.MatchesAnythingIn(command);

    private bool ShowsExtras => !IsSearching;

    private void OnSearchInput(ChangeEventArgs args)
    {
        SearchText = args.Value?.ToString() ?? string.Empty;
        _search = SettingSearch.For(SearchText);

        SelectFirstSearchHit();
    }

    private void SelectFirstSearchHit()
    {
        if (!IsSearching)
        {
            return;
        }

        if (VisibleCategories.FirstOrDefault(HasSearchHit) is { } hit)
        {
            SelectCategory(hit);
        }
        else if (CustomHasSearchHit)
        {
            SelectSpecial(CustomSection);
        }
    }

    public void SelectFirstConfiguredCategory()
    {
        var visible = VisibleCategories;

        SelectedCategory =
            visible.FirstOrDefault(category => SetCountIn(category) > 0) ??
            visible.FirstOrDefault();

        SelectedSpecial = null;
    }

    private void SelectCategory(SettingCategory category)
    {
        SelectedCategory = category;
        SelectedSpecial = null;
    }

    private void SelectSpecial(string section)
    {
        SelectedSpecial = section;
        SelectedCategory = null;
    }

    private Task Publish(LaunchOptions options)
    {
        _lastRendered = options.Format();
        RawDraft = _lastRendered;

        return OptionsChanged.InvokeAsync(options);
    }

    private Task ApplySetting(SettingDefinition definition, string? value) =>
        Publish(value is null
            ? Options.RemoveEnvironment(definition.Variable)
            : Options.SetEnvironment(definition.Variable, value));

    private Task ApplyWrapperCommand(string command, bool present) =>
        Publish(Options.WithWrapperCommand(command, present));

    private Task ApplyCpuAffinity(string? mask) => Publish(Options.WithCpuAffinity(mask));

    private Task RemoveCustomVariable(string name) => Publish(Options.RemoveEnvironment(name));

    private Task SetCustomVariable(string name, string? value) =>
        Publish(Options.SetEnvironment(name, value ?? string.Empty));

    private Task AddCustomVariable()
    {
        var name = NewVariableName.Trim();

        if (name.Length == 0)
        {
            return Task.CompletedTask;
        }

        var options = Options.SetEnvironment(name, NewVariableValue.Trim());

        NewVariableName = string.Empty;
        NewVariableValue = string.Empty;

        return Publish(options);
    }

    private Task OnRawInput(ChangeEventArgs args)
    {
        RawDraft = args.Value?.ToString() ?? string.Empty;

        var options = LaunchOptions.Parse(RawDraft);

        _lastRendered = options.Format();

        return OptionsChanged.InvokeAsync(options);
    }
}
