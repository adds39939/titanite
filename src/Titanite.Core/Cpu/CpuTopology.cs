namespace Titanite.Core.Cpu;

public sealed record CpuCacheGroup(IReadOnlyList<int> Threads, long CacheBytes)
{
    public string Mask => CpuAffinityMask.Format(Threads);
}

public sealed record CpuTopology
{
    public IReadOnlyList<int> AllThreads { get; init; } = [];

    public IReadOnlyList<CpuCacheGroup> CacheGroups { get; init; } = [];

    public IReadOnlyList<int> PhysicalCoreThreads { get; init; } = [];

    public bool HasSimultaneousMultithreading => PhysicalCoreThreads.Count < AllThreads.Count;

    public bool HasAsymmetricCache =>
        CacheGroups.Count > 1 &&
        CacheGroups.Select(group => group.CacheBytes).Distinct().Count() > 1;

    public CpuCacheGroup? LargestCacheGroup =>
        HasAsymmetricCache ? CacheGroups.MaxBy(group => group.CacheBytes) : null;

    public bool IsEmpty => AllThreads.Count == 0;
}
