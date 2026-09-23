using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Games;
using Titanite.Steam.Vdf;
using System.Runtime.CompilerServices;

namespace Titanite.Steam.Library;

internal sealed class SteamLibraryService(
    ISteamInstallLocator installLocator,
    ILogger<SteamLibraryService> logger) : ISteamLibraryService, IGameLibrary
{
    private const int StateFullyInstalled = 4;

    private const uint SteamworksCommonRedistributablesAppId = 228980;

    private const uint SteamControllerConfigsAppId = 353370;

    private static readonly HashSet<uint> KnownToolAppIds =
    [
        SteamworksCommonRedistributablesAppId,
        SteamControllerConfigsAppId
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<SteamApp>? _apps;

    private PublishedDetails? _published;

    public async Task<IReadOnlyList<SteamApp>> GetInstalledAppsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_apps is { } held)
        {
            return held;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return _apps ??= await Task.Run(() => ScanAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _apps = null;

    public async Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken cancellationToken = default) =>
        [.. (await GetInstalledAppsAsync(cancellationToken).ConfigureAwait(false)).Select(ToEntry)];

    private static GameEntry ToEntry(SteamApp app) => new()
    {
        Id = SteamIds.For(app.AppId),
        Name = app.Name,
        InstallDirectory = app.InstallDirectory,
        PrefixDirectory = SteamPaths.PrefixDirectory(app.LibraryPath, app.AppId),
        SizeOnDisk = app.SizeOnDisk,
        LastPlayed = app.LastPlayed,
        IsFullyInstalled = app.IsFullyInstalled,
        IsTool = app.Kind == SteamAppKind.Tool,
        RunsNatively = app.RunsNatively
    };

    private async Task<IReadOnlyList<SteamApp>> ScanAsync(CancellationToken cancellationToken)
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found on this machine.");

            return [];
        }

        logger.LogInformation("Scanning Steam installation at {SteamRoot}.", steamRoot);

        var manifests = new List<AppManifest>();

        foreach (var libraryPath in await GetLibraryPathsAsync(steamRoot, cancellationToken).ConfigureAwait(false))
        {
            await foreach (var manifest in ReadLibraryAsync(libraryPath, cancellationToken).ConfigureAwait(false))
            {
                manifests.Add(manifest);
            }
        }

        var metadata = ReadPublishedDetails(
            steamRoot,
            [.. manifests.Select(manifest => manifest.AppId).OfType<uint>()]);

        var entries = new List<SteamApp>();
        var seenAppIds = new HashSet<uint>();

        foreach (var manifest in manifests)
        {
            var entry = CreateEntry(manifest, metadata);

            if (entry is null)
            {
                logger.LogWarning("Skipped app manifest {ManifestPath}; it is missing required fields.", manifest.Path);

                continue;
            }

            if (seenAppIds.Add(entry.AppId))
            {
                entries.Add(entry);
            }
        }

        logger.LogInformation("Found {AppCount} installed Steam apps.", entries.Count);

        return
        [
            .. entries.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    private async Task<IReadOnlyList<string>> GetLibraryPathsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var paths = new List<string> { steamRoot };
        var seen = new HashSet<string>(StringComparer.Ordinal) { steamRoot };

        var manifestPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        var libraryFolders = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        if (libraryFolders is null)
        {
            logger.LogWarning("Could not read {ManifestPath}; only the root library will be scanned.", manifestPath);

            return paths;
        }

        foreach (var folder in libraryFolders.Properties())
        {
            var path = folder.Value switch
            {
                VObject details => details.GetString("path"),
                VValue value => value.Value?.ToString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && seen.Add(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private IReadOnlyDictionary<uint, SteamAppMetadata> ReadPublishedDetails(
        string steamRoot,
        HashSet<uint> appIds)
    {
        var file = new FileInfo(Path.Combine(steamRoot, "appcache", "appinfo.vdf"));
        var stamp = file.Exists ? (file.LastWriteTimeUtc, file.Length) : default;

        if (_published is { } held &&
            held.Path == file.FullName &&
            held.Stamp == stamp &&
            held.AppIds.IsSupersetOf(appIds))
        {
            return held.Metadata;
        }

        var metadata = SteamAppInfoFile.Read(file.FullName, appIds);

        logger.LogInformation("Read published details for {AppCount} Steam apps.", metadata.Count);

        _published = new PublishedDetails(file.FullName, stamp, appIds, metadata);

        return metadata;
    }

    private async IAsyncEnumerable<AppManifest> ReadLibraryAsync(
        string libraryPath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var steamAppsPath = Path.Combine(libraryPath, "steamapps");

        string[] manifestPaths;

        try
        {
            manifestPaths = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not list app manifests in {SteamAppsPath}.", steamAppsPath);

            yield break;
        }

        foreach (var manifestPath in manifestPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

            if (manifest is null)
            {
                logger.LogWarning("Skipped unreadable app manifest {ManifestPath}.", manifestPath);

                continue;
            }

            yield return new AppManifest(
                manifestPath,
                manifest,
                libraryPath,
                steamAppsPath,
                uint.TryParse(manifest.GetString("appid"), out var appId) ? appId : null);
        }
    }

    private static SteamApp? CreateEntry(
        AppManifest found,
        IReadOnlyDictionary<uint, SteamAppMetadata> metadata)
    {
        if (found.AppId is not { } appId)
        {
            return null;
        }

        var (_, manifest, libraryPath, steamAppsPath, _) = found;
        var installDirName = manifest.GetString("installdir");

        if (string.IsNullOrWhiteSpace(installDirName))
        {
            return null;
        }

        var installDirectory = Path.Combine(steamAppsPath, "common", installDirName);
        var published = metadata.GetValueOrDefault(appId);

        return new SteamApp
        {
            AppId = appId,
            Name = manifest.GetString("name") ?? installDirName,
            InstallDirectory = installDirectory,
            LibraryPath = libraryPath,
            Kind = ClassifyApp(appId, installDirectory, published),
            SizeOnDisk = manifest.GetInt64("SizeOnDisk"),
            LastPlayed = manifest.GetUnixTime("LastPlayed"),
            IsFullyInstalled = (manifest.GetInt64("StateFlags") & StateFullyInstalled) != 0,
            RunsNatively = published?.RunsOnLinux ?? false
        };
    }

    private static SteamAppKind ClassifyApp(uint appId, string installDirectory, SteamAppMetadata? published)
    {
        if (published?.Type is { Length: > 0 })
        {
            return published.IsTool ? SteamAppKind.Tool : SteamAppKind.Game;
        }

        if (KnownToolAppIds.Contains(appId))
        {
            return SteamAppKind.Tool;
        }

        return File.Exists(Path.Combine(installDirectory, "toolmanifest.vdf"))
            ? SteamAppKind.Tool
            : SteamAppKind.Game;
    }

    private sealed record AppManifest(
        string Path,
        VObject Manifest,
        string LibraryPath,
        string SteamAppsPath,
        uint? AppId);

    private sealed record PublishedDetails(
        string Path,
        (DateTime Modified, long Length) Stamp,
        HashSet<uint> AppIds,
        IReadOnlyDictionary<uint, SteamAppMetadata> Metadata);
}
