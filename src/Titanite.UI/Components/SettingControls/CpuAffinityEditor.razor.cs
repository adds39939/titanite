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

    private IReadOnlyList<AffinityPreset>? _presets;

    private IReadOnlyList<int> SelectedThreads { get; set; } = [];

    private HashSet<int> Selected { get; set; } = [];

    private IReadOnlyList<AffinityPreset> Presets => _presets ??= BuildPresets();

    protected override void OnParametersSet()
    {
        SelectedThreads = CpuAffinityMask.Parse(Value);
        Selected = [.. SelectedThreads];
    }

    private IReadOnlyList<AffinityPreset> BuildPresets()
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

    private bool IsSelected(AffinityPreset preset)
    {
        if (preset.Mask is null)
        {
            return string.IsNullOrWhiteSpace(Value);
        }

        return !string.IsNullOrWhiteSpace(Value) && preset.Threads.SequenceEqual(SelectedThreads);
    }

    private Task Apply(string? mask) => ValueChanged.InvokeAsync(mask);

    private Task ToggleThread(int thread, bool isOn)
    {
        var threads = Selected.ToHashSet();

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

    private sealed record AffinityPreset(string Label, string? Mask, string? Detail)
    {
        public IReadOnlyList<int> Threads { get; } = CpuAffinityMask.Parse(Mask);
    }
}
