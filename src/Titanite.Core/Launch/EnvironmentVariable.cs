namespace Titanite.Core.Launch;

public sealed record EnvironmentVariable(string Name, string Value)
{
    public string? OriginalText { get; init; }
}
