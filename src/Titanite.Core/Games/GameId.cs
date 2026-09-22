namespace Titanite.Core.Games;

public readonly record struct GameId(string Launcher, string Id)
{
    private const char Separator = ':';

    public bool IsEmpty => string.IsNullOrEmpty(Launcher) || string.IsNullOrEmpty(Id);

    public static bool TryParse(string? value, out GameId id)
    {
        id = default;

        if (value is null)
        {
            return false;
        }

        var separator = value.IndexOf(Separator);

        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        id = new GameId(value[..separator], value[(separator + 1)..]);

        return true;
    }

    public bool Equals(GameId other) =>
        string.Equals(Launcher, other.Launcher, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Id, other.Id, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(
        Launcher?.ToLowerInvariant(),
        Id);

    public override string ToString() => $"{Launcher}{Separator}{Id}";
}
