namespace Titanite.Core.Proton;

public sealed record ProtonSelection
{
    public required bool IsExplicit { get; init; }

    public string? ToolName { get; init; }

    public ProtonBuild? Build { get; init; }

    public bool IsMissing => ToolName is not null && Build is null;
}
