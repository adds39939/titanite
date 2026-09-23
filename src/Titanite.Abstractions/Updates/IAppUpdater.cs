namespace Titanite.Abstractions.Updates;

public interface IAppUpdater
{
    Task<AppUpdate?> CheckAsync(CancellationToken cancellationToken = default);

    Task<bool> InstallAsync(
        AppUpdate update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
