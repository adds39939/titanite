using Titanite.Abstractions.Launchers;
using Titanite.Core.Proton;

namespace Titanite.Steam.Proton;

internal interface IProtonToolService : ICompatibilityTools
{
    Task<CompatibilityToolAssignments> GetAssignmentsAsync(CancellationToken cancellationToken = default);
}
