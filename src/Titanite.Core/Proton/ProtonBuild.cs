namespace Titanite.Core.Proton;

public sealed record ProtonBuild
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string InstallPath { get; init; }

    public required ProtonBuildKind Kind { get; init; }

    public string? Version { get; init; }

    public uint AppId { get; init; }

    public bool NameIsDerived { get; init; }

    public ProtonCapabilities Capabilities { get; init; } = ProtonCapabilities.Unknown;
}
