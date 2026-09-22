using System.Text;

namespace Titanite.Steam.Vdf;

internal static class SteamConfigText
{
    public static string? GetValue(string document, IReadOnlyList<string> keyPath)
    {
        var scan = Scan(document, keyPath);

        return scan.ValueStart >= 0
            ? Unescape(document.Substring(scan.ValueStart, scan.ValueLength))
            : null;
    }

    private static ScanResult Scan(string document, IReadOnlyList<string> keyPath)
    {
        var result = new ScanResult();
        var path = new List<string>();
        var position = 0;

        while (position < document.Length)
        {
            SkipInsignificant(document, ref position);

            if (position >= document.Length)
            {
                break;
            }

            if (document[position] == '}')
            {
                if (path.Count > 0)
                {
                    path.RemoveAt(path.Count - 1);
                }

                position++;

                continue;
            }

            if (document[position] != '"')
            {
                position++;

                continue;
            }

            var key = ReadQuoted(document, ref position, out _, out _);

            SkipInsignificant(document, ref position);

            if (position < document.Length && document[position] == '{')
            {
                path.Add(key);
                position++;

                continue;
            }

            if (position < document.Length && document[position] == '"')
            {
                var isTarget = path.Count == keyPath.Count - 1 &&
                               IsPrefixOf(path, keyPath) &&
                               string.Equals(key, keyPath[^1], StringComparison.Ordinal);

                ReadQuoted(document, ref position, out var valueStart, out var valueLength);

                if (isTarget)
                {
                    result.ValueStart = valueStart;
                    result.ValueLength = valueLength;

                    return result;
                }
            }
        }

        return result;
    }

    private static bool IsPrefixOf(List<string> path, IReadOnlyList<string> keyPath)
    {
        if (path.Count >= keyPath.Count)
        {
            return false;
        }

        for (var i = 0; i < path.Count; i++)
        {
            if (!string.Equals(path[i], keyPath[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void SkipInsignificant(string document, ref int position)
    {
        while (position < document.Length)
        {
            if (char.IsWhiteSpace(document[position]))
            {
                position++;

                continue;
            }

            if (document[position] == '/' && position + 1 < document.Length && document[position + 1] == '/')
            {
                while (position < document.Length && document[position] != '\n')
                {
                    position++;
                }

                continue;
            }

            return;
        }
    }

    private static string ReadQuoted(string document, ref int position, out int start, out int length)
    {
        position++;
        start = position;

        while (position < document.Length && document[position] != '"')
        {
            position += document[position] == '\\' ? 2 : 1;
        }

        position = Math.Min(position, document.Length);
        length = position - start;

        var raw = document.Substring(start, length);

        if (position < document.Length)
        {
            position++;
        }

        return Unescape(raw);
    }

    public static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);

                continue;
            }

            builder.Append(value[++i] switch
            {
                'n' => '\n',
                't' => '\t',
                var other => other
            });
        }

        return builder.ToString();
    }

    private sealed class ScanResult
    {
        public int ValueStart { get; set; } = -1;

        public int ValueLength { get; set; }
    }
}
