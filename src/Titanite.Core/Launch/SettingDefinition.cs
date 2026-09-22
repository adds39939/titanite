using Titanite.Core.Proton;
using System.Text.RegularExpressions;

namespace Titanite.Core.Launch;

public enum SettingKind
{
    Toggle,

    Choice,

    Text,

    Number
}

public sealed record SettingDefinition(string Variable, SettingCategory Category, string Label)
{
    private Regex[]? _buildPatterns;

    public string? Description { get; init; }

    public string? Group { get; init; }

    public SettingKind Kind { get; init; } = SettingKind.Text;

    public string OnValue { get; init; } = "1";

    public IReadOnlyList<string> Choices { get; init; } = [];

    public string? Placeholder { get; init; }

    public CompoundSchema? Compound { get; init; }

    public IReadOnlyList<string> ProtonBuilds { get; init; } = [];

    public bool RestrictToProtonBuild { get; init; }

    public bool HideUnlessSet { get; init; }

    public bool AllowEmpty { get; init; }

    public bool IsOn(string? value) =>
        value is not null && !string.Equals(value, "0", StringComparison.Ordinal) && value.Length > 0;

    public bool AppliesTo(ProtonBuild? build)
    {
        if (ProtonBuilds.Count == 0 || build is null)
        {
            return true;
        }

        _buildPatterns ??= ProtonBuilds.Select(Compile).OfType<Regex>().ToArray();

        return _buildPatterns.Any(pattern =>
            pattern.IsMatch(build.Name) || (build.Version is { } version && pattern.IsMatch(version)));
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
