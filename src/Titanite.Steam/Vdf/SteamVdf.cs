using Gameloop.Vdf.Linq;
using Gameloop.Vdf;

namespace Titanite.Steam.Vdf;

internal static class SteamVdf
{
    private static readonly VdfSerializerSettings ReaderSettings = new()
    {
        MaximumTokenSize = 1 << 18,
        UsesEscapeSequences = true
    };

    public static async Task<VObject?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            return VdfConvert.Deserialize(text, ReaderSettings).Value as VObject;
        }
        catch (Exception e) when (e is IOException
                                      or UnauthorizedAccessException
                                      or VdfException
                                      or IndexOutOfRangeException)
        {
            return null;
        }
    }

    extension(VObject owner)
    {
        public string? GetString(string key)
        {
            foreach (var property in owner.Properties())
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    property.Value is VValue value)
                {
                    return value.Value?.ToString();
                }
            }

            return null;
        }

        public VObject? GetObject(string key)
        {
            foreach (var property in owner.Properties())
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    property.Value is VObject nested)
                {
                    return nested;
                }
            }

            return null;
        }

        public long GetInt64(string key) =>
            long.TryParse(owner.GetString(key), out var parsed) ? parsed : 0;

        public DateTimeOffset? GetUnixTime(string key)
        {
            var seconds = owner.GetInt64(key);

            return seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        }
    }
}
