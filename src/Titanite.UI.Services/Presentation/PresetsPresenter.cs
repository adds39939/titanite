using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;

namespace Titanite.UI.Services.Presentation;

public sealed class PresetsPresenter(IPresetService presets, ICompatibilityTools compatibilityTools)
    : IPresetsPresenter
{
    private string _savedOptions = string.Empty;

    private string _savedCompatTool = CompatibilityTool.Inherit;

    public IReadOnlyList<Preset> Presets { get; private set; } = [Preset.Global];

    public string SelectedId { get; private set; } = PresetId.Global;

    public LaunchOptions Editing { get; private set; } = new();

    public string CompatTool { get; private set; } = CompatibilityTool.Inherit;

    public ProtonCatalogue ProtonBuilds { get; private set; } = ProtonCatalogue.Empty;

    public bool IsLoading { get; private set; } = true;

    public bool IsSaving { get; private set; }

    public StatusMessage? Status { get; private set; }

    public int AppliedCount { get; private set; }

    public string SavedOptions => _savedOptions;

    public Preset Selected =>
        Presets.FirstOrDefault(preset => PresetId.Same(preset.Id, SelectedId)) ?? Preset.Global;

    public bool CanDelete => Selected.CanBeRemoved;

    public ProtonBuild? EffectiveBuild =>
        CompatTool.Length > 0 ? ProtonBuilds.FindBuild(CompatTool) : null;

    public bool CompatToolChanged =>
        !string.Equals(CompatTool, _savedCompatTool, StringComparison.OrdinalIgnoreCase);

    public bool HasChanges =>
        !string.Equals(Editing.Format(), _savedOptions, StringComparison.Ordinal) || CompatToolChanged;

    public bool HasAnythingToReset => _savedOptions.Length > 0 || _savedCompatTool.Length > 0;

    public IReadOnlyList<string> PendingSideEffects
    {
        get
        {
            var changes = new List<string>();

            if (CompatToolChanged)
            {
                changes.Add(CompatTool.Length == 0
                    ? "Stop choosing a Proton build, leaving that to the launcher."
                    : $"Run every game using this preset under {EffectiveBuild?.DisplayName ?? CompatTool}.");
            }

            if (AppliedCount > 0)
            {
                changes.Add($"Apply these settings to the {AppliedCount} {Games(AppliedCount)} using this preset.");
            }

            return changes;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Presets = await presets.GetAllAsync(cancellationToken);
            ProtonBuilds = await compatibilityTools.GetCatalogueAsync(cancellationToken);

            await ShowAsync(SelectedId, cancellationToken);
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"Could not read the presets: {e.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectAsync(string id, CancellationToken cancellationToken = default)
    {
        if (PresetId.Same(id, SelectedId))
        {
            return;
        }

        Status = null;

        await ShowAsync(id, cancellationToken);
    }

    public async Task CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        IsSaving = true;
        Status = null;

        try
        {
            var created = await presets.CreateAsync(name, cancellationToken);

            Presets = await presets.GetAllAsync(cancellationToken);

            await ShowAsync(created.Id, cancellationToken);

            Status = StatusMessage.Success($"Created {created.Name}. Nothing uses it yet.");
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The preset could not be created: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task RenameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!CanDelete || PresetName.Clean(name) is not { Length: > 0 })
        {
            return;
        }

        IsSaving = true;
        Status = null;

        try
        {
            var renamed = await presets.RenameAsync(SelectedId, name, cancellationToken);

            Presets = await presets.GetAllAsync(cancellationToken);
            SelectedId = renamed.Id;
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The preset could not be renamed: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanDelete)
        {
            return;
        }

        IsSaving = true;
        Status = null;

        var removed = Selected.Name;

        try
        {
            await presets.DeleteAsync(SelectedId, cancellationToken);

            Presets = await presets.GetAllAsync(cancellationToken);

            await ShowAsync(PresetId.Global, cancellationToken);

            Status = StatusMessage.Success(
                $"Removed {removed}. The games that used it keep the settings it left behind.");
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The preset could not be removed: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void Edit(LaunchOptions options)
    {
        Editing = options;
        Status = null;
    }

    public void ChooseCompatibilityTool(string toolName)
    {
        CompatTool = toolName;
        Status = null;
    }

    public void Revert()
    {
        Editing = LaunchOptions.Parse(_savedOptions);
        CompatTool = _savedCompatTool;
        Status = null;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        IsSaving = true;
        Status = null;

        try
        {
            var result = await presets.SaveAndApplyAsync(
                Selected with { Options = Editing, CompatibilityTool = CompatTool },
                cancellationToken);

            if (result.IsSuccess)
            {
                Presets = await presets.GetAllAsync(cancellationToken);
                _savedOptions = Editing.Format();
                _savedCompatTool = CompatTool;

                Status = StatusMessage.Success(AppliedCount == 0
                    ? "Saved."
                    : $"Saved and applied to {AppliedCount} {Games(AppliedCount)}.");
            }
            else
            {
                Status = StatusMessage.Error(result.Message ?? "The preset could not be saved.");
            }
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The preset could not be saved: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        IsSaving = true;

        try
        {
            await presets.ResetAsync(SelectedId, cancellationToken);

            Presets = await presets.GetAllAsync(cancellationToken);

            await ShowAsync(SelectedId, cancellationToken);

            Status = StatusMessage.Success(
                "The preset is empty and no game uses it. Their own settings are untouched.");
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The preset could not be reset: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task ShowAsync(string id, CancellationToken cancellationToken)
    {
        SelectedId = Presets.Any(preset => PresetId.Same(preset.Id, id)) ? id : PresetId.Global;

        var preset = Selected;

        Editing = preset.Options;
        CompatTool = preset.CompatibilityTool;
        _savedOptions = Editing.Format();
        _savedCompatTool = CompatTool;

        AppliedCount = (await presets.GamesUsingAsync(SelectedId, cancellationToken)).Count;
    }

    private static string Games(int count) => count == 1 ? "game" : "games";
}
