using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;

namespace Titanite.UI.Services.Presentation;

public interface IGameConfigurationPresenter
{
    const string NoPreset = "No preset";

    GameEntry? Entry { get; }

    bool IsLoading { get; }

    string? LoadError { get; }

    bool IsSaving { get; }

    StatusMessage? Status { get; }

    LaunchOptions Editing { get; }

    string SavedOptions { get; }

    IReadOnlyList<Preset> Presets { get; }

    string? AppliedPresetId { get; }

    Preset? AppliedPreset { get; }

    string PresetLabel { get; }

    string CompatTool { get; }

    ProtonCatalogue ProtonBuilds { get; }

    ProtonBuild? EffectiveBuild { get; }

    ProtonCapabilities Capabilities { get; }

    CompatibilityToolAssignments Assignments { get; }

    string LauncherName { get; }

    bool CanLaunch { get; }

    bool CompatToolChanged { get; }

    bool PresetChanged { get; }

    bool HasChanges { get; }

    bool HasAnythingToReset { get; }

    IReadOnlyList<string> PendingSideEffects { get; }

    string Describe(GameId id);

    Task LoadAsync(GameId id, CancellationToken cancellationToken = default);

    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);

    void Edit(LaunchOptions options);

    Task UsePresetAsync(string? presetId, CancellationToken cancellationToken = default);

    void ChooseCompatibilityTool(string toolName);

    void Revert();

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);

    Task CopyFromAsync(GameEntry source, CancellationToken cancellationToken = default);

    bool WarnBeforeLaunching();

    void Launch();

    void OpenInstallDirectory();

    void OpenPrefixDirectory();
}
