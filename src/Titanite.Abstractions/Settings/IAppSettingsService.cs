using Titanite.Core.Settings;

namespace Titanite.Abstractions.Settings;

public interface IAppSettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
