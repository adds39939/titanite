namespace Titanite.Core.Proton;

public sealed record ProtonCapabilities
{
    private const string ReadablePrefix = "PROTON_";

    public static ProtonCapabilities Unknown { get; } = new()
    {
        Variables = new HashSet<string>(StringComparer.Ordinal),
        IsKnown = false
    };

    public required IReadOnlySet<string> Variables { get; init; }

    public required bool IsKnown { get; init; }

    public bool? Reads(string variable) =>
        IsKnown && variable.StartsWith(ReadablePrefix, StringComparison.Ordinal)
            ? Variables.Contains(variable)
            : null;

    public bool Ignores(string variable) => Reads(variable) is false;
}
