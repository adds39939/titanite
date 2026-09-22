using Titanite.Core.Proton;

namespace Titanite.Abstractions.Launchers;

public interface ICompatibilityTools
{
    Task<ProtonCatalogue> GetCatalogueAsync(CancellationToken cancellationToken = default);
}
