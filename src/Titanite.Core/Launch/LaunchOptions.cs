using System.Text.RegularExpressions;

namespace Titanite.Core.Launch;

public sealed partial record LaunchOptions
{
    public const string CommandPlaceholder = "%command%";

    public IReadOnlyList<EnvironmentVariable> Environment { get; init; } = [];

    public IReadOnlyList<string> Wrapper { get; init; } = [];

    public bool HasCommandPlaceholder { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    private IReadOnlyList<string> OriginalOrder { get; init; } = [];

    public bool IsEmpty =>
        Environment.Count == 0 && Wrapper.Count == 0 && Arguments.Count == 0 && !HasCommandPlaceholder;

    public static LaunchOptions Parse(string? launchOptions)
    {
        var tokens = ShellTokenizer.TokenizeWithSource(launchOptions ?? string.Empty);

        var environment = new List<EnvironmentVariable>();
        var index = 0;

        while (index < tokens.Count && AssignmentPattern().IsMatch(tokens[index].Text))
        {
            var (text, rawText) = tokens[index];
            var separator = text.IndexOf('=');

            environment.Add(new EnvironmentVariable(text[..separator], text[(separator + 1)..])
            {
                OriginalText = rawText
            });

            index++;
        }

        var rest = tokens.Skip(index).Select(token => token.Text).ToList();
        var placeholder = rest.IndexOf(CommandPlaceholder);

        return new LaunchOptions
        {
            Environment = environment,
            OriginalOrder = environment.Select(variable => variable.Name).ToList(),
            Wrapper = placeholder >= 0 ? rest[..placeholder] : [],
            HasCommandPlaceholder = placeholder >= 0,
            Arguments = placeholder >= 0 ? rest[(placeholder + 1)..] : rest
        };
    }

    public string Format() => string.Join(' ', FormatTokens());

    public IReadOnlyList<string> FormatTokens()
    {
        var parts = new List<string>(Environment.Count + Wrapper.Count + Arguments.Count + 1);

        parts.AddRange(Environment.Select(FormatAssignment));

        parts.AddRange(Wrapper.Select(ShellTokenizer.Quote));

        if (HasCommandPlaceholder)
        {
            parts.Add(CommandPlaceholder);
        }

        parts.AddRange(Arguments.Select(ShellTokenizer.Quote));

        return parts;
    }

    private static string FormatAssignment(EnvironmentVariable variable)
    {
        if (variable.OriginalText is { } original &&
            ShellTokenizer.TokenizeWithSource(original) is [var only] &&
            only.Text == $"{variable.Name}={variable.Value}")
        {
            return original;
        }

        return variable.Value.Length == 0
            ? $"{variable.Name}="
            : $"{variable.Name}={ShellTokenizer.Quote(variable.Value)}";
    }

    public LaunchOptions SetEnvironment(string name, string value)
    {
        var wasEmpty = IsEmpty;
        var environment = Environment.ToList();
        var index = environment.FindIndex(variable => string.Equals(variable.Name, name, StringComparison.Ordinal));

        if (index >= 0)
        {
            environment[index] = environment[index] with { Value = value };
        }
        else
        {
            environment.Insert(PositionFor(name, environment), new EnvironmentVariable(name, value));
        }

        return this with { Environment = environment, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

    public LaunchOptions RemoveEnvironment(string name) =>
        this with
        {
            Environment = Environment
                .Where(variable => !string.Equals(variable.Name, name, StringComparison.Ordinal))
                .ToList()
        };

    private int PositionFor(string name, IReadOnlyList<EnvironmentVariable> environment)
    {
        var original = IndexInOriginal(name);

        if (original < 0)
        {
            return environment.Count;
        }

        return environment.Count(variable => IndexInOriginal(variable.Name) is >= 0 and var other &&
                                             other < original);
    }

    private int IndexInOriginal(string name)
    {
        for (var index = 0; index < OriginalOrder.Count; index++)
        {
            if (string.Equals(OriginalOrder[index], name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public EnvironmentVariable? FindEnvironment(string name) =>
        Environment.FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.Ordinal));

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*=")]
    private static partial Regex AssignmentPattern();
}
