namespace Titanite.Core.Launch;

public sealed class SettingCatalog
{
    private readonly Dictionary<string, SettingDefinition> _byVariable;
    private readonly Dictionary<string, IReadOnlyList<SettingDefinition>> _byCategory;

    public SettingCatalog(
        IEnumerable<SettingCategory> categories,
        IEnumerable<SettingDefinition> definitions)
    {
        Categories = categories.OrderBy(category => category.Order).ToList();
        All = [.. definitions];

        _byVariable = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);

        foreach (var definition in All)
        {
            _byVariable.TryAdd(definition.Variable, definition);
        }

        _byCategory = new Dictionary<string, IReadOnlyList<SettingDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in Categories)
        {
            _byCategory.TryAdd(category.Id, Collect(category));
        }
    }

    public static SettingCatalog Empty { get; } = new([], []);

    public IReadOnlyList<SettingCategory> Categories { get; }

    public IReadOnlyList<SettingDefinition> All { get; }

    public SettingDefinition? Find(string variable) => _byVariable.GetValueOrDefault(variable);

    public IReadOnlyList<SettingDefinition> In(SettingCategory category) =>
        _byCategory.TryGetValue(category.Id, out var held) ? held : Collect(category);

    private List<SettingDefinition> Collect(SettingCategory category) =>
        All.Where(definition => definition.Category.Is(category.Id)).ToList();

    public IReadOnlyList<SettingGroup> GroupsIn(SettingCategory category) =>
        Group(In(category));

    public static IReadOnlyList<SettingGroup> Group(IEnumerable<SettingDefinition> definitions)
    {
        var groups = new List<SettingGroup>();
        var current = new List<SettingDefinition>();
        string? name = null;

        foreach (var definition in definitions)
        {
            if (current.Count > 0 && !string.Equals(definition.Group, name, StringComparison.Ordinal))
            {
                groups.Add(new SettingGroup(name, current));
                current = [];
            }

            name = definition.Group;
            current.Add(definition);
        }

        if (current.Count > 0)
        {
            groups.Add(new SettingGroup(name, current));
        }

        return groups;
    }

    public SettingCategory? FindCategory(string id) =>
        Categories.FirstOrDefault(category => category.Is(id));
}
