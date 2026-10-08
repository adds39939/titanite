using Titanite.Core.Cpu;

namespace Titanite.Core.Launch;

public sealed partial record LaunchOptions
{
    public const string TasksetCommand = "taskset";
    public const string WineCpuTopologyVariable = "WINE_CPU_TOPOLOGY";

    private static readonly string[] TasksetListFlags = ["-c", "--cpu-list"];

    public string? CpuAffinity => TasksetAffinity ?? WineCpuTopologyAffinity;

    public CpuAffinityMethod? AffinityMethod
    {
        get
        {
            if (TasksetAffinity is not null)
            {
                return CpuAffinityMethod.Taskset;
            }

            return WineCpuTopologyAffinity is not null ? CpuAffinityMethod.WineCpuTopology : null;
        }
    }

    private string? TasksetAffinity
    {
        get
        {
            var index = IndexOfCommand(TasksetCommand);

            return index >= 0 && index + 2 < Wrapper.Count && TasksetListFlags.Contains(Wrapper[index + 1])
                ? Wrapper[index + 2]
                : null;
        }
    }

    private string? WineCpuTopologyAffinity
    {
        get
        {
            var value = FindEnvironment(WineCpuTopologyVariable)?.Value ?? string.Empty;
            var separator = value.IndexOf(':');

            if (separator < 0)
            {
                return null;
            }

            var mask = CpuAffinityMask.Format(CpuAffinityMask.Parse(value[(separator + 1)..]));

            return mask.Length > 0 ? mask : null;
        }
    }

    public LaunchOptions WithCpuAffinity(string? mask, CpuAffinityMethod method)
    {
        if (string.IsNullOrWhiteSpace(mask))
        {
            return WithoutTaskset().RemoveEnvironment(WineCpuTopologyVariable);
        }

        return method == CpuAffinityMethod.Taskset
            ? RemoveEnvironment(WineCpuTopologyVariable).WithTaskset(mask.Trim())
            : WithoutTaskset().WithWineCpuTopology(mask);
    }

    private LaunchOptions WithoutTaskset()
    {
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(TasksetCommand, wrapper);

        if (index < 0)
        {
            return this;
        }

        var hasList = index + 2 < wrapper.Count && TasksetListFlags.Contains(wrapper[index + 1]);

        wrapper.RemoveRange(index, hasList ? 3 : 1);

        return this with { Wrapper = wrapper };
    }

    private LaunchOptions WithTaskset(string mask)
    {
        var wasEmpty = IsEmpty;
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(TasksetCommand, wrapper);
        var hasList = index >= 0 && index + 2 < wrapper.Count && TasksetListFlags.Contains(wrapper[index + 1]);

        if (hasList)
        {
            wrapper[index + 2] = mask;
        }
        else
        {
            if (index >= 0)
            {
                wrapper.RemoveAt(index);
            }

            wrapper.AddRange([TasksetCommand, "-c", mask]);
        }

        return this with { Wrapper = wrapper, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

    private LaunchOptions WithWineCpuTopology(string mask)
    {
        var threads = CpuAffinityMask.Parse(mask);

        return threads.Count == 0
            ? RemoveEnvironment(WineCpuTopologyVariable)
            : SetEnvironment(WineCpuTopologyVariable, $"{threads.Count}:{string.Join(',', threads)}");
    }
}
