using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Abstractions.Presets;
using Titanite.Core.Games;
using Titanite.Core.Presets;
using System.Text.Json;

namespace Titanite.Storage.Presets;

public sealed class PresetService(
    ITitaniteStorage storage,
    ILaunchOptionsStore launchOptions,
    IGameLauncher launcher,
    ILogger<PresetService> logger) : IPresetService
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private StoredPresets? _presets;

    public async Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).InOrder();

    public async Task<Preset> GetAsync(string id, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken).ConfigureAwait(false))
        .FirstOrDefault(preset => PresetId.Same(preset.Id, id))
        ?? Preset.Global;

    public async Task<Preset> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);

        var clean = PresetName.Clean(name) is { Length: > 0 } given ? given : "New preset";

        var created = new Preset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = PresetName.Unique(clean, stored.InOrder())
        };

        await StoreAsync(
                stored with { Presets = [.. stored.Presets, StoredPreset.From(created)] },
                cancellationToken)
            .ConfigureAwait(false);

        return created;
    }

    public async Task<Preset> RenameAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing.IsGlobal || PresetName.Clean(name) is not { Length: > 0 } clean)
        {
            return existing;
        }

        var others = stored.InOrder().Where(preset => !PresetId.Same(preset.Id, id));
        var renamed = existing with { Name = PresetName.Unique(clean, others) };

        await StoreAsync(stored with { Presets = Replace(stored.Presets, renamed) }, cancellationToken)
            .ConfigureAwait(false);

        return renamed;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (PresetId.IsGlobal(id))
        {
            return;
        }

        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);

        await StoreAsync(
                stored with
                {
                    Presets = [.. stored.Presets.Where(preset => !PresetId.Same(preset.Id, id))],
                    Applied = stored.Applied
                        .Where(pair => !PresetId.Same(pair.Value, id))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
        Preset preset,
        CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var games = GamesUsing(stored, preset.Id);

        if (games.Count == 0)
        {
            await StoreAsync(stored with { Presets = Replace(stored.Presets, preset) }, cancellationToken)
                .ConfigureAwait(false);

            return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
        }

        var result = await launchOptions.SaveManyAsync(
                games.ToDictionary(game => game, _ => preset.Options),
                games.ToDictionary(game => game, _ => preset.CompatibilityTool),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await StoreAsync(stored with { Presets = Replace(stored.Presets, preset) }, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Applied the preset {PresetName} to {GameCount} games.",
                preset.Name,
                games.Count);
        }

        return result;
    }

    public async Task ResetAsync(string id, CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var preset = await GetAsync(id, cancellationToken).ConfigureAwait(false);

        var emptied = preset with { Options = new(), CompatibilityTool = string.Empty };

        await StoreAsync(
                stored with
                {
                    Presets = Replace(stored.Presets, emptied),
                    Applied = stored.Applied
                        .Where(pair => !PresetId.Same(pair.Value, id))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> AppliedToAsync(GameId id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false))
        .Applied
        .GetValueOrDefault(id.ToString());

    public async Task ApplyAsync(GameId id, string? presetId, CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var applied = stored.Applied.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var key = id.ToString();

        var known = presetId is { Length: > 0 } &&
                    stored.Presets.Any(preset => PresetId.Same(preset.Id, presetId));

        if (known)
        {
            applied[key] = presetId!;
        }
        else if (!applied.Remove(key))
        {
            return;
        }

        await StoreAsync(stored with { Applied = applied }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<GameId, string>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).Assignments();

    public async Task<IReadOnlyList<GameId>> GamesUsingAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        GamesUsing(await LoadAsync(cancellationToken).ConfigureAwait(false), id);

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);

        if (stored.Applied.Count == 0)
        {
            return 0;
        }

        var byId = stored.InOrder().ToDictionary(preset => preset.Id, StringComparer.OrdinalIgnoreCase);
        var assignments = stored.Assignments();

        var actual = await launchOptions
            .GetManyAsync([.. assignments.Keys], cancellationToken)
            .ConfigureAwait(false);

        var builds = await launcher
            .GetCompatibilityToolAssignmentsAsync(cancellationToken)
            .ConfigureAwait(false);

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (game, presetId) in assignments)
        {
            var options = actual.GetValueOrDefault(game) ?? new();

            if (byId.TryGetValue(presetId, out var preset) &&
                preset.Matches(options, builds.For(game) ?? string.Empty))
            {
                kept[game.ToString()] = presetId;
            }
            else
            {
                logger.LogInformation(
                    "{GameId} no longer matches the preset it had, so it no longer uses one.",
                    game);
            }
        }

        var dropped = stored.Applied.Count - kept.Count;

        if (dropped > 0)
        {
            await StoreAsync(stored with { Applied = kept }, cancellationToken).ConfigureAwait(false);
        }

        return dropped;
    }

    private static IReadOnlyList<GameId> GamesUsing(StoredPresets stored, string id) =>
    [
        .. stored.Assignments()
            .Where(pair => PresetId.Same(pair.Value, id))
            .Select(pair => pair.Key)
            .OrderBy(game => game.ToString(), StringComparer.Ordinal)
    ];

    private static IReadOnlyList<StoredPreset> Replace(IReadOnlyList<StoredPreset> presets, Preset preset)
    {
        var stored = StoredPreset.From(preset);

        return presets.Any(candidate => PresetId.Same(candidate.Id, preset.Id))
            ? [.. presets.Select(candidate => PresetId.Same(candidate.Id, preset.Id) ? stored : candidate)]
            : [.. presets, stored];
    }

    private async Task<StoredPresets> LoadAsync(CancellationToken cancellationToken)
    {
        if (_presets is not null)
        {
            return _presets;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return _presets ??= await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<StoredPresets> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(storage.PresetsFile))
            {
                var json = await File.ReadAllTextAsync(storage.PresetsFile, cancellationToken)
                    .ConfigureAwait(false);

                return (JsonSerializer.Deserialize<StoredPresets>(json, StoredPresetsContext.Presets)
                        ?? StoredPresets.Empty).Sanitised();
            }

            if (File.Exists(storage.ProfileFile))
            {
                return await MigrateAsync(cancellationToken).ConfigureAwait(false);
            }

            return StoredPresets.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(e, "Could not read the presets at {PresetsPath}.", storage.PresetsFile);

            return StoredPresets.Empty;
        }
    }

    private async Task<StoredPresets> MigrateAsync(CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(storage.ProfileFile, cancellationToken).ConfigureAwait(false);

        var profile = (JsonSerializer.Deserialize<StoredProfile>(json, StoredProfileContext.Profile)
                       ?? new StoredProfile()).Upgraded();

        logger.LogInformation(
            "Carried the global profile and its {GameCount} games over to the Global preset.",
            profile.LinkedGames.Count);

        return StoredPresets.FromProfile(profile).Sanitised();
    }

    private async Task StoreAsync(StoredPresets presets, CancellationToken cancellationToken)
    {
        var sanitised = presets.Sanitised();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(storage.Root);

            await File.WriteAllTextAsync(
                storage.PresetsFile,
                JsonSerializer.Serialize(sanitised, StoredPresetsContext.Presets),
                cancellationToken).ConfigureAwait(false);

            _presets = sanitised;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write the presets to {PresetsPath}.", storage.PresetsFile);

            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
