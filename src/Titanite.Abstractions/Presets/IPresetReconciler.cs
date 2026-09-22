namespace Titanite.Abstractions.Presets;

public interface IPresetReconciler
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
