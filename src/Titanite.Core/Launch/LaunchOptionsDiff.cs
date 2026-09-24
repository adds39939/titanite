namespace Titanite.Core.Launch;

public enum LaunchDiffKind
{
    Unchanged,
    Added,
    Removed
}

public sealed record LaunchDiffToken(string Text, LaunchDiffKind Kind);

public static class LaunchOptionsDiff
{
    public static IReadOnlyList<LaunchDiffToken> Compare(LaunchOptions saved, LaunchOptions pending) =>
        Compare(saved.FormatTokens(), pending.FormatTokens());

    public static IReadOnlyList<LaunchDiffToken> Compare(
        IReadOnlyList<string> saved,
        IReadOnlyList<string> pending)
    {
        var common = new int[saved.Count + 1, pending.Count + 1];

        for (var i = saved.Count - 1; i >= 0; i--)
        {
            for (var j = pending.Count - 1; j >= 0; j--)
            {
                common[i, j] = string.Equals(saved[i], pending[j], StringComparison.Ordinal)
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var tokens = new List<LaunchDiffToken>();
        var savedIndex = 0;
        var pendingIndex = 0;

        while (savedIndex < saved.Count && pendingIndex < pending.Count)
        {
            if (string.Equals(saved[savedIndex], pending[pendingIndex], StringComparison.Ordinal))
            {
                tokens.Add(new LaunchDiffToken(pending[pendingIndex], LaunchDiffKind.Unchanged));
                savedIndex++;
                pendingIndex++;
            }
            else if (common[savedIndex + 1, pendingIndex] >= common[savedIndex, pendingIndex + 1])
            {
                tokens.Add(new LaunchDiffToken(saved[savedIndex], LaunchDiffKind.Removed));
                savedIndex++;
            }
            else
            {
                tokens.Add(new LaunchDiffToken(pending[pendingIndex], LaunchDiffKind.Added));
                pendingIndex++;
            }
        }

        while (savedIndex < saved.Count)
        {
            tokens.Add(new LaunchDiffToken(saved[savedIndex++], LaunchDiffKind.Removed));
        }

        while (pendingIndex < pending.Count)
        {
            tokens.Add(new LaunchDiffToken(pending[pendingIndex++], LaunchDiffKind.Added));
        }

        return tokens;
    }
}
