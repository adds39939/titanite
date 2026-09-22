namespace Titanite.Core.Games;

public sealed record GameEntry
{
    public required GameId Id { get; init; }

    public required string Name { get; init; }

    public required string InstallDirectory { get; init; }

    public string? PrefixDirectory { get; init; }

    public long SizeOnDisk { get; init; }

    public DateTimeOffset? LastPlayed { get; init; }

    public bool IsFullyInstalled { get; init; }

    public bool IsTool { get; init; }

    public bool RunsNatively { get; init; }
}
