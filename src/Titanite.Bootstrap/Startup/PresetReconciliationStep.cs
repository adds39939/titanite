using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Presets;

namespace Titanite.Bootstrap.Startup;

internal sealed class PresetReconciliationStep(IPresetReconciler reconciler) : IStartupStep
{
    public string Name => "The preset check";

    public string Activity => "Checking presets against Steam…";

    public Task RunAsync(CancellationToken cancellationToken = default) =>
        reconciler.StartAsync(cancellationToken);
}
