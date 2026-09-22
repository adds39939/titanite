using Titanite.Core.Cpu;

namespace Titanite.Abstractions.Cpu;

public interface ICpuTopologyService
{
    CpuTopology Get();
}
