using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Cpu;
using Titanite.Core.Cpu;

namespace Titanite.Platform.Cpu;

public sealed class LinuxCpuTopologyService(ILogger<LinuxCpuTopologyService> logger) : ICpuTopologyService
{
    private const string CpuRoot = "/sys/devices/system/cpu";

    private const string LastLevelCache = "cache/index3";

    private CpuTopology? _topology;

    public CpuTopology Get() => _topology ??= Read();

    private CpuTopology Read()
    {
        try
        {
            var threads = Directory
                .EnumerateDirectories(CpuRoot, "cpu*")
                .Select(directory => Path.GetFileName(directory)[3..])
                .Where(name => name.All(char.IsAsciiDigit) && name.Length > 0)
                .Select(int.Parse)
                .Order()
                .ToList();

            if (threads.Count == 0)
            {
                logger.LogWarning("No processors were found under {CpuRoot}.", CpuRoot);

                return new CpuTopology();
            }

            var topology = new CpuTopology
            {
                AllThreads = threads,
                CacheGroups = ReadCacheGroups(threads),
                PhysicalCoreThreads = ReadPhysicalCoreThreads(threads)
            };

            logger.LogInformation(
                "Detected {ThreadCount} threads in {GroupCount} cache groups; asymmetric cache: {Asymmetric}.",
                topology.AllThreads.Count,
                topology.CacheGroups.Count,
                topology.HasAsymmetricCache);

            return topology;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            logger.LogWarning(e, "Could not read the processor topology.");

            return new CpuTopology();
        }
    }

    private IReadOnlyList<CpuCacheGroup> ReadCacheGroups(IEnumerable<int> threads)
    {
        var groups = new Dictionary<string, CpuCacheGroup>(StringComparer.Ordinal);

        foreach (var thread in threads)
        {
            var shared = ReadValue($"{CpuRoot}/cpu{thread}/{LastLevelCache}/shared_cpu_list");

            if (shared is null || groups.ContainsKey(shared))
            {
                continue;
            }

            groups[shared] = new CpuCacheGroup(
                CpuAffinityMask.Parse(shared),
                ParseCacheSize(ReadValue($"{CpuRoot}/cpu{thread}/{LastLevelCache}/size")));
        }

        return groups.Values.OrderBy(group => group.Threads.FirstOrDefault()).ToList();
    }

    private IReadOnlyList<int> ReadPhysicalCoreThreads(IEnumerable<int> threads)
    {
        var cores = new HashSet<string>(StringComparer.Ordinal);
        var primary = new List<int>();

        foreach (var thread in threads)
        {
            var siblings = ReadValue($"{CpuRoot}/cpu{thread}/topology/thread_siblings_list");

            if (siblings is null || cores.Add(siblings))
            {
                primary.Add(thread);
            }
        }

        return primary;
    }

    private static string? ReadValue(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static long ParseCacheSize(string? size)
    {
        if (string.IsNullOrEmpty(size))
        {
            return 0;
        }

        var multiplier = char.ToUpperInvariant(size[^1]) switch
        {
            'K' => 1024L,
            'M' => 1024L * 1024,
            'G' => 1024L * 1024 * 1024,
            _ => 1L
        };

        var digits = multiplier == 1 ? size : size[..^1];

        return long.TryParse(digits, out var value) ? value * multiplier : 0;
    }
}
