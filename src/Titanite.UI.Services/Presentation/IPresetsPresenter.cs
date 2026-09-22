using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;

namespace Titanite.UI.Services.Presentation;

public interface IPresetsPresenter
{
    IReadOnlyList<Preset> Presets { get; }

    string SelectedId { get; }

    Preset Selected { get; }

    LaunchOptions Editing { get; }

    string CompatTool { get; }

    ProtonCatalogue ProtonBuilds { get; }

    ProtonBuild? EffectiveBuild { get; }

    bool IsLoading { get; }

    bool IsSaving { get; }

    StatusMessage? Status { get; }

    int AppliedCount { get; }

    string SavedOptions { get; }

    bool CanDelete { get; }

    bool CompatToolChanged { get; }

    bool HasChanges { get; }

    bool HasAnythingToReset { get; }

    IReadOnlyList<string> PendingSideEffects { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SelectAsync(string id, CancellationToken cancellationToken = default);

    Task CreateAsync(string name, CancellationToken cancellationToken = default);

    Task RenameAsync(string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);

    void Edit(LaunchOptions options);

    void ChooseCompatibilityTool(string toolName);

    void Revert();

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
