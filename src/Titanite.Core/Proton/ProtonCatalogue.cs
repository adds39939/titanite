namespace Titanite.Core.Proton;

public sealed record ProtonCatalogue
{
    public static ProtonCatalogue Empty { get; } = new() { Builds = [] };

    public required IReadOnlyList<ProtonBuild> Builds { get; init; }

    public ProtonBuild? FindBuild(string? toolName) =>
        toolName is null
            ? null
            : Builds.FirstOrDefault(build => string.Equals(build.Name, toolName, StringComparison.OrdinalIgnoreCase));

    public ProtonSelection Resolve(string? explicitToolName, string? defaultToolName)
    {
        if (explicitToolName is not null)
        {
            return new ProtonSelection
            {
                IsExplicit = true,
                ToolName = explicitToolName,
                Build = FindBuild(explicitToolName)
            };
        }

        return new ProtonSelection
        {
            IsExplicit = false,
            ToolName = defaultToolName,
            Build = FindBuild(defaultToolName)
        };
    }

    public IReadOnlyList<string> MissingToolNames(CompatibilityToolAssignments assignments) =>
        [.. assignments
            .AllToolNames
            .Where(name => FindBuild(name) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
}
