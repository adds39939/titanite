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
    private const int ReconcileAttempts = 3;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly SemaphoreSlim _updating = new(1, 1);

    private StoredPresets? _presets;
    private long _version;

    public async Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).InOrder();

    public async Task<Preset> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Find(await LoadAsync(cancellationToken).ConfigureAwait(false), id);

    public Task<Preset> CreateAsync(string name, CancellationToken cancellationToken = default) =>
        UpdateAsync(stored =>
        {
            var clean = PresetName.Clean(name) is { Length: > 0 } given
                ? given 
                : "New preset";

            var created = new Preset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = PresetName.Unique(clean, stored.InOrder())
            };

            return (stored with { Presets = [.. stored.Presets, StoredPreset.From(created)] }, created);
        }, cancellationToken);

    public Task<Preset> RenameAsync(string id, string name, CancellationToken cancellationToken = default) =>
        UpdateAsync(stored =>
        {
            var existing = Find(stored, id);

            if (existing.IsGlobal || PresetName.Clean(name) is not { Length: > 0 } clean)
            {
                return (null, existing);
            }

            var others = stored.InOrder().Where(preset => !PresetId.Same(preset.Id, id));
            var renamed = existing with { Name = PresetName.Unique(clean, others) };

            return (stored with { Presets = Replace(stored.Presets, renamed) }, renamed);
        }, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (PresetId.IsGlobal(id))
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(stored => stored with
        {
            Presets = [.. stored.Presets.Where(preset => !PresetId.Same(preset.Id, id))],
            Applied = WithoutPreset(stored.Applied, id)
        }, cancellationToken);
    }

    public Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
        Preset preset,
        CancellationToken cancellationToken = default) =>
        UpdateAsync<LaunchOptionsSaveResult>(async stored =>
        {
            var games = GamesUsing(stored, preset.Id);
            var saved = stored with { Presets = Replace(stored.Presets, preset) };

            if (games.Count == 0)
            {
                return (saved, new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));
            }

            var result = await launchOptions.SaveManyAsync(
                    games.ToDictionary(game => game, _ => preset.Options),
                    games.ToDictionary(game => game, _ => preset.CompatibilityTool),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return (null, result);
            }

            logger.LogInformation(
                "Applied the preset {PresetName} to {GameCount} games.",
                preset.Name,
                games.Count);

            return (saved, result);
        }, cancellationToken);

    public Task ResetAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateAsync(stored =>
        {
            var emptied = Find(stored, id) with { Options = new(), CompatibilityTool = string.Empty };

            return stored with
            {
                Presets = Replace(stored.Presets, emptied),
                Applied = WithoutPreset(stored.Applied, id)
            };
        }, cancellationToken);

    public async Task<string?> AppliedToAsync(GameId id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false))
        .Applied
        .GetValueOrDefault(id.ToString());

    public Task ApplyAsync(GameId id, string? presetId, CancellationToken cancellationToken = default) =>
        UpdateAsync(stored =>
        {
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
                return null;
            }

            return stored with { Applied = applied };
        }, cancellationToken);

    public async Task<IReadOnlyDictionary<GameId, string>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).Assignments();

    public async Task<IReadOnlyList<GameId>> GamesUsingAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        GamesUsing(await LoadAsync(cancellationToken).ConfigureAwait(false), id);

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= ReconcileAttempts; attempt++)
        {
            var version = Interlocked.Read(ref _version);
            var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);

            if (stored.Applied.Count == 0)
            {
                return 0;
            }

            var kept = await KeptAsync(stored, cancellationToken).ConfigureAwait(false);
            var dropped = stored.Applied.Count - kept.Count;

            if (dropped == 0)
            {
                return 0;
            }

            var committed = await UpdateAsync(latest => Interlocked.Read(ref _version) == version
                    ? (latest with { Applied = kept }, true)
                    : (null, false),
                cancellationToken).ConfigureAwait(false);

            if (committed)
            {
                return dropped;
            }

            logger.LogDebug("The presets changed while they were being checked, so they will be checked again.");
        }

        return 0;
    }

    private async Task<Dictionary<string, string>> KeptAsync(
        StoredPresets stored,
        CancellationToken cancellationToken)
    {
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

        return kept;
    }

    private Task UpdateAsync(Func<StoredPresets, StoredPresets?> change, CancellationToken cancellationToken) =>
        UpdateAsync(stored => (change(stored), true), cancellationToken);

    private Task<T> UpdateAsync<T>(
        Func<StoredPresets, (StoredPresets? Changed, T Result)> change,
        CancellationToken cancellationToken) =>
        UpdateAsync<T>(stored => Task.FromResult(change(stored)), cancellationToken);

    private async Task<T> UpdateAsync<T>(
        Func<StoredPresets, Task<(StoredPresets? Changed, T Result)>> change,
        CancellationToken cancellationToken)
    {
        await _updating.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var (changed, result) = await change(await LoadAsync(cancellationToken).ConfigureAwait(false))
                .ConfigureAwait(false);

            if (changed is not null)
            {
                await StoreAsync(changed, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _updating.Release();
        }
    }

    private static Preset Find(StoredPresets stored, string id) =>
        stored.InOrder().FirstOrDefault(preset => PresetId.Same(preset.Id, id)) ?? Preset.Global;

    private static Dictionary<string, string> WithoutPreset(IReadOnlyDictionary<string, string> applied, string id) =>
        applied
            .Where(pair => !PresetId.Same(pair.Value, id))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

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
            Interlocked.Increment(ref _version);
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
