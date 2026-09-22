using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Launchers;
using Titanite.Core.Proton;
using Titanite.Steam.Library;
using Titanite.Steam.Vdf;
using System.Text.RegularExpressions;

namespace Titanite.Steam.Proton;

internal sealed partial class ProtonToolService(
    ISteamInstallLocator installLocator,
    ISteamLibraryService library,
    ILogger<ProtonToolService> logger) : IProtonToolService
{
    private const uint DefaultAppId = 0;

    private const string ProtonLayerName = "proton";

    private const string RegistrationMarker = "Registering tool ";

    public async Task<ProtonCatalogue> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found, so no Proton builds can be listed.");

            return ProtonCatalogue.Empty;
        }

        var registeredNames = await ReadRegisteredNamesAsync(steamRoot, cancellationToken).ConfigureAwait(false);

        var builds = new List<ProtonBuild>();
        builds.AddRange(await ReadValveBuildsAsync(registeredNames, cancellationToken).ConfigureAwait(false));
        builds.AddRange(await ReadCustomBuildsAsync(steamRoot, cancellationToken).ConfigureAwait(false));

        logger.LogInformation("Found {BuildCount} installed Proton builds.", builds.Count);

        return new ProtonCatalogue
        {
            Builds =
            [
                .. builds
                    .OrderBy(build => build.Kind)
                    .ThenBy(build => build.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            ]
        };
    }

    public async Task<CompatibilityToolAssignments> GetAssignmentsAsync(
        CancellationToken cancellationToken = default)
    {
        if (installLocator.Locate() is not { } steamRoot)
        {
            return CompatibilityToolAssignments.None;
        }

        var mappings = await ReadMappingsAsync(steamRoot, cancellationToken).ConfigureAwait(false);

        return new CompatibilityToolAssignments
        {
            ByGame = mappings
                .Where(pair => pair.Key != DefaultAppId)
                .ToDictionary(pair => SteamIds.For(pair.Key), pair => pair.Value),
            Default = mappings.GetValueOrDefault(DefaultAppId)
        };
    }

    private async Task<IReadOnlyList<ProtonBuild>> ReadValveBuildsAsync(
        IReadOnlyDictionary<uint, string> registeredNames,
        CancellationToken cancellationToken)
    {
        var apps = await library.GetInstalledAppsAsync(cancellationToken).ConfigureAwait(false);
        var builds = new List<ProtonBuild>();

        foreach (var app in apps)
        {
            if (app.Kind != SteamAppKind.Tool ||
                !await IsProtonAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var registered = registeredNames.GetValueOrDefault(app.AppId);

            if (registered is null)
            {
                logger.LogInformation(
                    "Steam's compatibility log does not name app {AppId} ({AppName}), so its internal name was inferred.",
                    app.AppId,
                    app.Name);
            }

            builds.Add(new ProtonBuild
            {
                Name = registered ?? ProtonToolName.Derive(app.Name),
                DisplayName = app.Name,
                InstallPath = app.InstallDirectory,
                Kind = ProtonBuildKind.Valve,
                Version = await ReadVersionAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false),
                AppId = app.AppId,
                NameIsDerived = registered is null,
                Capabilities = await ProbeAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false)
            });
        }

        return builds;
    }

    private async Task<IReadOnlyList<ProtonBuild>> ReadCustomBuildsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var toolsPath = Path.Combine(steamRoot, "compatibilitytools.d");
        string[] directories;

        try
        {
            if (!Directory.Exists(toolsPath))
            {
                return [];
            }

            directories = Directory.GetDirectories(toolsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not list {ToolsPath}; builds installed by hand will be missing.", toolsPath);

            return [];
        }

        var builds = new List<ProtonBuild>();

        foreach (var directory in directories)
        {
            var manifestPath = Path.Combine(directory, "compatibilitytool.vdf");
            var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var tools = manifest?.GetObject("compat_tools");

            if (tools is null)
            {
                logger.LogInformation("Skipped {Directory}; it declares no compatibility tools.", directory);

                continue;
            }

            foreach (var property in tools.Properties())
            {
                if (property.Value is not VObject tool)
                {
                    continue;
                }

                var installPath = Path.GetFullPath(tool.GetString("install_path") ?? ".", directory);

                if (!await IsProtonAsync(installPath, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogInformation("Skipped {ToolName}; it is a compatibility tool but not Proton.", property.Key);

                    continue;
                }

                builds.Add(new ProtonBuild
                {
                    Name = property.Key,
                    DisplayName = tool.GetString("display_name") ?? property.Key,
                    InstallPath = installPath,
                    Kind = ProtonBuildKind.Custom,
                    Version = await ReadVersionAsync(installPath, cancellationToken).ConfigureAwait(false),
                    Capabilities = await ProbeAsync(installPath, cancellationToken).ConfigureAwait(false)
                });
            }
        }

        return builds;
    }

    private async Task<IReadOnlyDictionary<uint, string>> ReadMappingsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var configPath = SteamCompatTools.ConfigPathIn(steamRoot);
        var document = await SteamVdf.TryReadAsync(configPath, cancellationToken).ConfigureAwait(false);

        var mappings = SteamCompatTools.MappingRoot
            .Skip(1)
            .Aggregate(document, (node, key) => node?.GetObject(key));

        var result = new Dictionary<uint, string>();

        if (mappings is null)
        {
            logger.LogInformation("No compatibility tool mappings were found in {ConfigPath}.", configPath);

            return result;
        }

        foreach (var property in mappings.Properties())
        {
            if (!uint.TryParse(property.Key, out var appId) || property.Value is not VObject entry)
            {
                continue;
            }

            var toolName = entry.GetString(SteamCompatTools.NameKey);

            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            result[appId] = toolName;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<uint, string>> ReadRegisteredNamesAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var logPath = Path.Combine(steamRoot, "logs", "compat_log.txt");
        var names = new Dictionary<uint, string>();

        try
        {
            if (!File.Exists(logPath))
            {
                logger.LogInformation("No compatibility log at {LogPath}; build names will be inferred.", logPath);

                return names;
            }

            using var reader = new StreamReader(logPath);

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (!line.Contains(RegistrationMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                var registration = ToolRegistration().Match(line);

                if (!registration.Success ||
                    !uint.TryParse(registration.Groups["appId"].ValueSpan, out var appId) ||
                    appId == 0)
                {
                    continue;
                }

                names[appId] = registration.Groups["name"].Value;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {LogPath}; build names will be inferred.", logPath);
        }

        return names;
    }

    private static async Task<bool> IsProtonAsync(string installPath, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(installPath, "toolmanifest.vdf");
        var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        return string.Equals(
            manifest?.GetString("compatmanager_layer_name"),
            ProtonLayerName,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProtonCapabilities> ProbeAsync(string installPath, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(installPath, "proton");

        try
        {
            if (!File.Exists(scriptPath))
            {
                logger.LogWarning("{InstallPath} has no proton script, so its settings cannot be checked.", installPath);

                return ProtonCapabilities.Unknown;
            }

            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);

            var variables = ProtonVariable()
                .Matches(script)
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal);

            return new ProtonCapabilities { Variables = variables, IsKnown = true };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {ScriptPath}; its settings will not be checked.", scriptPath);

            return ProtonCapabilities.Unknown;
        }
    }

    private static async Task<string?> ReadVersionAsync(string installPath, CancellationToken cancellationToken)
    {
        var versionPath = Path.Combine(installPath, "version");

        try
        {
            if (!File.Exists(versionPath))
            {
                return null;
            }

            var text = (await File.ReadAllTextAsync(versionPath, cancellationToken).ConfigureAwait(false)).Trim();
            var separator = text.IndexOf(' ');

            return separator >= 0 ? text[(separator + 1)..] : text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"Registering tool (?<name>.+), AppID (?<appId>\d+)\s*$")]
    private static partial Regex ToolRegistration();

    [GeneratedRegex("PROTON_[A-Z0-9_]+")]
    private static partial Regex ProtonVariable();
}
