using System.Text;

namespace Titanite.Core.Launch;

public readonly record struct ShellToken(string Text, string RawText);

public static class ShellTokenizer
{
    private const string MustQuote = " \t\n\r\"'\\$`;&|<>()#!*?[]{}~";

    public static IReadOnlyList<string> Tokenize(string commandLine) =>
        TokenizeWithSource(commandLine).Select(token => token.Text).ToList();

    public static IReadOnlyList<ShellToken> TokenizeWithSource(string commandLine)
    {
        var tokens = new List<ShellToken>();
        var current = new StringBuilder();
        var start = 0;
        var started = false;
        var inSingleQuotes = false;
        var inDoubleQuotes = false;

        void Begin(int index)
        {
            if (started)
            {
                return;
            }

            started = true;
            start = index;
        }

        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (!inSingleQuotes && !inDoubleQuotes && char.IsWhiteSpace(c))
            {
                if (started)
                {
                    tokens.Add(new ShellToken(current.ToString(), commandLine[start..i]));
                    current.Clear();
                    started = false;
                }

                continue;
            }

            Begin(i);

            if (c == '\\' && !inSingleQuotes && i + 1 < commandLine.Length)
            {
                var next = commandLine[i + 1];

                if (inDoubleQuotes && next is not ('"' or '\\' or '$' or '`'))
                {
                    current.Append(c);
                }
                else
                {
                    current.Append(next);
                    i++;
                }

                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;

                continue;
            }

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;

                continue;
            }

            current.Append(c);
        }

        if (started)
        {
            tokens.Add(new ShellToken(current.ToString(), commandLine[start..]));
        }

        return tokens;
    }

    public static string Quote(string token)
    {
        if (token.Length == 0)
        {
            return "\"\"";
        }

        if (!token.Any(MustQuote.Contains))
        {
            return token;
        }

        var quoted = new StringBuilder(token.Length + 2).Append('"');

        foreach (var c in token)
        {
            if (c is '"' or '\\' or '$' or '`')
            {
                quoted.Append('\\');
            }

            quoted.Append(c);
        }

        return quoted.Append('"').ToString();
    }
}
