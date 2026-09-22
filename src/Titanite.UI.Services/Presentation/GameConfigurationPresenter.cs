using Titanite.Abstractions.Desktop;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Launch;
using Titanite.Core.Presets;
using Titanite.Core.Proton;
using Titanite.UI.Services.Formatting;

namespace Titanite.UI.Services.Presentation;

public sealed class GameConfigurationPresenter(
    ILaunchOptionsStore launchOptions,
    IGameLibrary library,
    IPresetService presets,
    ICompatibilityTools compatibilityTools,
    IGameLauncher launcher,
    IFileManagerService fileManager) : IGameConfigurationPresenter
{
    private GameId _loaded;

    private string _savedOptions = string.Empty;

    private string _savedCompatTool = CompatibilityTool.Inherit;

    private string? _savedPreset;

    public GameEntry? Entry { get; private set; }

    public bool IsLoading { get; private set; } = true;

    public string? LoadError { get; private set; }

    public bool IsSaving { get; private set; }

    public StatusMessage? Status { get; private set; }

    public LaunchOptions Editing { get; private set; } = new();

    public string SavedOptions => _savedOptions;

    public IReadOnlyList<Preset> Presets { get; private set; } = [Preset.Global];

    public string? AppliedPresetId { get; private set; }

    public Preset? AppliedPreset =>
        AppliedPresetId is { Length: > 0 } id
            ? Presets.FirstOrDefault(preset => PresetId.Same(preset.Id, id))
            : null;

    public string PresetLabel => AppliedPreset?.Name ?? IGameConfigurationPresenter.NoPreset;

    public string CompatTool { get; private set; } = CompatibilityTool.Inherit;

    public ProtonCatalogue ProtonBuilds { get; private set; } = ProtonCatalogue.Empty;

    public CompatibilityToolAssignments Assignments { get; private set; } =
        CompatibilityToolAssignments.None;

    public string LauncherName => launcher.Name;

    public string Describe(GameId id) =>
        id.IsEmpty ? string.Empty : $"{LauncherLabelFor(id)} · {id.Id}";

    private string LauncherLabelFor(GameId id) =>
        string.Equals(id.Launcher, launcher.Key, StringComparison.OrdinalIgnoreCase)
            ? launcher.Name
            : id.Launcher;

    public bool CanLaunch => launcher.Capabilities.CanLaunchGames;

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Entry is not { } entry || IsLoading || IsSaving)
        {
            return false;
        }

        var stored = await launchOptions.GetAsync(entry.Id, cancellationToken);
        var assignments = await launcher.GetCompatibilityToolAssignmentsAsync(cancellationToken);
        var tool = assignments.For(entry.Id) ?? CompatibilityTool.Inherit;

        var optionsMoved = !string.Equals(stored.Format(), _savedOptions, StringComparison.Ordinal);
        var toolMoved = !string.Equals(tool, _savedCompatTool, StringComparison.OrdinalIgnoreCase);

        if (!optionsMoved && !toolMoved)
        {
            return false;
        }

        var wasEditing = HasChanges;

        Assignments = assignments;
        _savedOptions = stored.Format();
        _savedCompatTool = tool;

        _savedPreset = await presets.AppliedToAsync(entry.Id, cancellationToken);

        if (wasEditing)
        {
            Status = StatusMessage.Warning(
                $"{launcher.Name} has different settings for this game now. Your unsaved changes are " +
                "still here; saving overwrites what it has.");

            return true;
        }

        Editing = stored;
        CompatTool = tool;
        AppliedPresetId = _savedPreset;
        Status = StatusMessage.Warning($"These settings were changed outside Titanite.");

        return true;
    }

    public ProtonBuild? EffectiveBuild => CompatTool.Length > 0
        ? ProtonBuilds.FindBuild(CompatTool)
        : ProtonBuilds.FindBuild(Assignments.Default);

    public ProtonCapabilities Capabilities => EffectiveBuild?.Capabilities ?? ProtonCapabilities.Unknown;

    public bool CompatToolChanged =>
        !string.Equals(CompatTool, _savedCompatTool, StringComparison.OrdinalIgnoreCase);

    public bool PresetChanged => !PresetId.Same(AppliedPresetId, _savedPreset);

    public bool HasChanges =>
        !string.Equals(Editing.Format(), _savedOptions, StringComparison.Ordinal) ||
        PresetChanged ||
        CompatToolChanged;

    public bool HasAnythingToReset => _savedOptions.Length > 0 || _savedPreset is { Length: > 0 };

    public IReadOnlyList<string> PendingSideEffects
    {
        get
        {
            var changes = new List<string>();

            if (CompatToolChanged)
            {
                changes.Add(CompatTool.Length == 0
                    ? $"Let {launcher.Name} choose the Proton build, rather than the one set now."
                    : $"Run the game under {EffectiveBuild?.DisplayName ?? CompatTool}.");
            }

            if (PresetChanged)
            {
                changes.Add(AppliedPreset is { } preset
                    ? $"Use the {preset.Name} preset from now on, so a change to it reaches this game."
                    : "Stop using a preset, keeping the settings it put here.");
            }

            return changes;
        }
    }

    public async Task LoadAsync(GameId id, CancellationToken cancellationToken = default)
    {
        if (_loaded == id && !IsLoading)
        {
            return;
        }

        _loaded = id;
        IsLoading = true;
        LoadError = null;
        Status = null;
        Entry = null;

        try
        {
            Entry = id.IsEmpty
                ? null
                : (await library.GetGamesAsync(cancellationToken)).FirstOrDefault(game => game.Id == id);

            if (Entry is null)
            {
                return;
            }

            Editing = await launchOptions.GetAsync(id, cancellationToken);
            _savedOptions = Editing.Format();

            Presets = await presets.GetAllAsync(cancellationToken);

            _savedPreset = await presets.AppliedToAsync(id, cancellationToken);
            AppliedPresetId = _savedPreset;

            ProtonBuilds = await compatibilityTools.GetCatalogueAsync(cancellationToken);
            Assignments = await launcher.GetCompatibilityToolAssignmentsAsync(cancellationToken);

            _savedCompatTool = Assignments.For(id) ?? CompatibilityTool.Inherit;
            CompatTool = _savedCompatTool;
        }
        catch (Exception e)
        {
            Editing = new LaunchOptions();
            LoadError = $"Could not read launch options: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Edit(LaunchOptions options)
    {
        Editing = options;
        AppliedPresetId = null;
        Status = null;
    }

    public async Task UsePresetAsync(string? presetId, CancellationToken cancellationToken = default)
    {
        AppliedPresetId = presetId is { Length: > 0 } ? presetId : null;
        Status = null;

        if (AppliedPresetId is null)
        {
            return;
        }

        var preset = await presets.GetAsync(AppliedPresetId, cancellationToken);

        Editing = preset.Options;
        CompatTool = preset.CompatibilityTool;
    }

    public void ChooseCompatibilityTool(string toolName)
    {
        CompatTool = toolName;
        AppliedPresetId = null;
        Status = null;
    }

    public void Revert()
    {
        Editing = LaunchOptions.Parse(_savedOptions);
        AppliedPresetId = _savedPreset;
        CompatTool = _savedCompatTool;
        Status = null;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Entry is not { } entry)
        {
            return;
        }

        IsSaving = true;
        Status = null;

        try
        {
            var result = await launchOptions.SaveManyAsync(
                new Dictionary<GameId, LaunchOptions> { [entry.Id] = Editing },
                CompatToolChanged
                    ? new Dictionary<GameId, string> { [entry.Id] = CompatTool }
                    : new Dictionary<GameId, string>(),
                cancellationToken);

            if (result.IsSuccess)
            {
                _savedOptions = Editing.Format();
                _savedCompatTool = CompatTool;

                await presets.ApplyAsync(entry.Id, AppliedPresetId, cancellationToken);
                _savedPreset = AppliedPresetId;

                Status = StatusMessage.Success("Saved.");
            }
            else
            {
                Status = StatusMessage.Error(result.Message ?? "The launch options could not be saved.");
            }
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The launch options could not be saved: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Entry is not { } entry)
        {
            return;
        }

        IsSaving = true;
        Status = null;

        try
        {
            var result = await launchOptions.SaveAsync(entry.Id, new LaunchOptions(), cancellationToken);

            if (result.IsSuccess)
            {
                await presets.ApplyAsync(entry.Id, null, cancellationToken);

                Editing = new LaunchOptions();
                _savedOptions = string.Empty;
                AppliedPresetId = null;
                _savedPreset = null;

                Status = StatusMessage.Success("Reset. The game has no launch options.");
            }
            else
            {
                Status = StatusMessage.Error(result.Message ?? "The game could not be reset.");
            }
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"The game could not be reset: {e.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task CopyFromAsync(GameEntry source, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await PresetOnAsync(source.Id, cancellationToken) is { } preset)
            {
                await UsePresetAsync(preset.Id, cancellationToken);

                Status = StatusMessage.Success(
                    $"Copied from {source.Name}, which uses {preset.Name}. This game will follow it " +
                    "too. Nothing is written until you save.");

                return;
            }

            Editing = await launchOptions.GetAsync(source.Id, cancellationToken);
            CompatTool = Assignments.For(source.Id) ?? CompatibilityTool.Inherit;
            AppliedPresetId = null;

            Status = StatusMessage.Success($"Copied from {source.Name}. Nothing is written until you save.");
        }
        catch (Exception e)
        {
            Status = StatusMessage.Error($"Could not read {source.Name}: {e.Message}");
        }
    }

    private async Task<Preset?> PresetOnAsync(GameId id, CancellationToken cancellationToken) =>
        await presets.AppliedToAsync(id, cancellationToken) is { Length: > 0 } applied
            ? Presets.FirstOrDefault(preset => PresetId.Same(preset.Id, applied))
            : null;

    public bool WarnBeforeLaunching()
    {
        if (Entry is null || IsSaving || !HasChanges)
        {
            return false;
        }

        Status = StatusMessage.Warning(
            $"There are unsaved changes. Launching now runs the game as {launcher.Name} has it " +
            "stored. Launch again to go ahead, or save first.");

        return true;
    }

    public void Launch()
    {
        if (Entry is not { } entry || IsSaving)
        {
            return;
        }

        Status = launcher.Launch(entry.Id)
            ? StatusMessage.Success($"Asked {launcher.Name} to launch the game.")
            : StatusMessage.Error($"{launcher.Name} could not be asked to launch the game.");
    }

    public void OpenInstallDirectory()
    {
        if (Entry is not { } entry)
        {
            return;
        }

        OpenDirectory(
            entry.InstallDirectory,
            $"The game's install folder is not there. {launcher.Name} may have moved or removed it.");
    }

    public void OpenPrefixDirectory()
    {
        if (Entry?.PrefixDirectory is not { } prefix)
        {
            return;
        }

        OpenDirectory(
            prefix,
            "The game has no Wine prefix yet. Proton builds one the first time the game runs.");
    }

    private void OpenDirectory(string path, string missingMessage)
    {
        var status = fileManager.OpenDirectory(path);

        Status = status switch
        {
            DirectoryOpenStatus.Opened => null,
            DirectoryOpenStatus.NotFound => StatusMessage.Error(missingMessage),
            _ => StatusMessage.Error($"Could not open {PathDisplay.Abbreviate(path)}.")
        };
    }
}
