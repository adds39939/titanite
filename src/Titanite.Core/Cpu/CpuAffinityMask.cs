using System.Text;

namespace Titanite.Core.Cpu;

public static class CpuAffinityMask
{
    public static string Format(IEnumerable<int> threads)
    {
        var sorted = threads.Distinct().Order().ToList();

        if (sorted.Count == 0)
        {
            return string.Empty;
        }

        var mask = new StringBuilder();
        var runStart = sorted[0];

        for (var i = 0; i < sorted.Count; i++)
        {
            var isLast = i == sorted.Count - 1;

            if (!isLast && sorted[i + 1] == sorted[i] + 1)
            {
                continue;
            }

            if (mask.Length > 0)
            {
                mask.Append(',');
            }

            mask.Append(runStart == sorted[i] ? $"{runStart}" : $"{runStart}-{sorted[i]}");

            if (!isLast)
            {
                runStart = sorted[i + 1];
            }
        }

        return mask.ToString();
    }

    public static IReadOnlyList<int> Parse(string? mask)
    {
        var threads = new SortedSet<int>();

        foreach (var section in (mask ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
        {
            if (section.Length == 0)
            {
                continue;
            }

            var dash = section.IndexOf('-');

            if (dash < 0)
            {
                if (int.TryParse(section, out var single))
                {
                    threads.Add(single);
                }

                continue;
            }

            if (int.TryParse(section[..dash], out var from) &&
                int.TryParse(section[(dash + 1)..], out var to) &&
                to >= from)
            {
                for (var thread = from; thread <= to; thread++)
                {
                    threads.Add(thread);
                }
            }
        }

        return threads.ToList();
    }
}
