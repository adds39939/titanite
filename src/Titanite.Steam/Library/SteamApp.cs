namespace Titanite.Steam.Library;

internal sealed record SteamApp
{
    public required uint AppId { get; init; }

    public required string Name { get; init; }

    public required string InstallDirectory { get; init; }

    public required string LibraryPath { get; init; }

    public required SteamAppKind Kind { get; init; }

    public long SizeOnDisk { get; init; }

    public DateTimeOffset? LastPlayed { get; init; }

    public bool IsFullyInstalled { get; init; }

    public bool RunsNatively { get; init; }
}
