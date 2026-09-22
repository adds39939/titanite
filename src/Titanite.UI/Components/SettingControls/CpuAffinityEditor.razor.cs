using Microsoft.AspNetCore.Components;
using Titanite.Abstractions.Cpu;
using Titanite.Core.Cpu;

namespace Titanite.UI.Components.SettingControls;

public partial class CpuAffinityEditor : ComponentBase
{
    [Inject]
    private ICpuTopologyService TopologyService { get; set; } = null!;

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private CpuTopology Topology => TopologyService.Get();

    private IReadOnlyList<int> SelectedThreads => CpuAffinityMask.Parse(Value);

    private IReadOnlyList<AffinityPreset> Presets
    {
        get
        {
            var presets = new List<AffinityPreset>
            {
                new("All threads", null, "No pinning; the scheduler decides.")
            };

            foreach (var group in Topology.CacheGroups)
            {
                var isLargest = Topology.LargestCacheGroup == group;

                presets.Add(new AffinityPreset(
                    isLargest ? "Largest cache group" : "Cache group",
                    group.Mask,
                    $"{group.Threads.Count} threads sharing {group.CacheBytes / 1024 / 1024} MiB" +
                    (isLargest ? " — usually the one games want" : string.Empty)));
            }

            if (Topology.HasSimultaneousMultithreading)
            {
                presets.Add(new AffinityPreset(
                    "Physical cores only",
                    CpuAffinityMask.Format(Topology.PhysicalCoreThreads),
                    "One thread per core, avoiding sibling threads."));
            }

            return presets;
        }
    }

    private bool IsSelected(AffinityPreset preset)
    {
        if (preset.Mask is null)
        {
            return string.IsNullOrWhiteSpace(Value);
        }

        return !string.IsNullOrWhiteSpace(Value) &&
               CpuAffinityMask.Parse(preset.Mask).SequenceEqual(SelectedThreads);
    }

    private Task Apply(string? mask) => ValueChanged.InvokeAsync(mask);

    private Task ToggleThread(int thread, bool isOn)
    {
        var threads = SelectedThreads.ToHashSet();

        if (isOn)
        {
            threads.Add(thread);
        }
        else
        {
            threads.Remove(thread);
        }

        return Apply(threads.Count == 0 || threads.Count == Topology.AllThreads.Count
            ? null
            : CpuAffinityMask.Format(threads));
    }

    private Task OnMaskChanged(ChangeEventArgs args)
    {
        var mask = args.Value?.ToString();

        return Apply(string.IsNullOrWhiteSpace(mask) ? null : mask.Trim());
    }

    private sealed record AffinityPreset(string Label, string? Mask, string? Detail);
}
