using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Processes;
using Titanite.Abstractions.Updates;

namespace Titanite.Platform.Updates;

public sealed class GitHubReleaseUpdater : IAppUpdater
{
    private const string BundleExtension = ".flatpak";

    private const string InstallCommand = "flatpak";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    private readonly HttpClient _http;
    private readonly IApplicationInfo _application;
    private readonly IHostProcesses _host;
    private readonly ILogger<GitHubReleaseUpdater> _logger;
    private readonly string _downloadDirectory;

    public GitHubReleaseUpdater(
        HttpClient http,
        IApplicationInfo application,
        IHostProcesses host,
        IOptions<UpdaterOptions> options,
        ILogger<GitHubReleaseUpdater> logger)
    {
        _http = http;
        _application = application;
        _host = host;
        _logger = logger;
        _downloadDirectory = options.Value.DownloadDirectory;
    }

    public async Task<AppUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_application.IsDevelopment)
        {
            _logger.LogInformation("Not checking for updates in development.");

            return null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(CheckTimeout);

            using var response = await _http
                .GetAsync(ReleasesAddress(_application.RepositoryUrl), timeout.Token)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var releases = await response.Content
                .ReadFromJsonAsync<List<GitHubRelease>>(JsonOptions, timeout.Token)
                .ConfigureAwait(false) ?? [];

            if (FindRunningRelease(_application.Version, releases) is null)
            {
                _logger.LogInformation(
                    "No release is tagged for Titanite {Version}, so the latest release is treated as an update.",
                    _application.Version);
            }

            var update = SelectUpdate(_application.Version, releases, RuntimeInformation.OSArchitecture);

            if (update is null)
            {
                _logger.LogInformation("Titanite {Version} is up to date.", _application.Version);
            }
            else
            {
                _logger.LogInformation("Titanite {Version} is available.", update.Version);
            }

            return update;
        }
        catch (Exception e) when (
            e is HttpRequestException or JsonException ||
            (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(e, "Could not check for updates.");

            return null;
        }
    }

    public async Task<bool> InstallAsync(
        AppUpdate update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var bundle = Path.Combine(_downloadDirectory, Path.GetFileName(update.BundleName));

        try
        {
            _logger.LogInformation("Downloading Titanite {Version} from {Address}.", update.Version, update.BundleUrl);

            Directory.CreateDirectory(_downloadDirectory);
            await DownloadAsync(update.BundleUrl, bundle, progress, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Installing Titanite {Version}.", update.Version);

            var installed = await _host
                .RunAsync(InstallCommand, ["install", "--user", "--noninteractive", "--reinstall", bundle], cancellationToken)
                .ConfigureAwait(false);

            if (installed)
            {
                _logger.LogInformation("Installed Titanite {Version}.", update.Version);
            }

            return installed;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Could not download Titanite {Version}.", update.Version);

            return false;
        }
        finally
        {
            TryDelete(bundle);
        }
    }

    internal static Uri ReleasesAddress(Uri repository) =>
        new($"https://api.github.com/repos{repository.AbsolutePath.TrimEnd('/')}/releases?per_page=30");

    internal static AppUpdate? SelectUpdate(
        string runningVersion,
        IReadOnlyList<GitHubRelease> releases,
        Architecture architecture)
    {
        var running = FindRunningRelease(runningVersion, releases);
        var latest = Published(releases).MaxBy(release => release.PublishedAt);

        if (latest is null || (running is not null && latest.PublishedAt <= running.PublishedAt))
        {
            return null;
        }

        var platform = architecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => null
        };

        var bundle = platform is null
            ? null
            : latest.Assets.FirstOrDefault(asset =>
                asset.Name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase) &&
                asset.Name.Contains(platform, StringComparison.Ordinal) &&
                asset.DownloadUrl.Scheme == Uri.UriSchemeHttps);

        return bundle is null
            ? null
            : new AppUpdate(latest.TagName.TrimStart('v', 'V'), bundle.Name, bundle.DownloadUrl);
    }

    internal static GitHubRelease? FindRunningRelease(string runningVersion, IReadOnlyList<GitHubRelease> releases) =>
        Published(releases).FirstOrDefault(release =>
            string.Equals(release.TagName.TrimStart('v', 'V'), runningVersion, StringComparison.Ordinal));

    private static IEnumerable<GitHubRelease> Published(IEnumerable<GitHubRelease> releases) =>
        releases.Where(release => !release.Draft && release.PublishedAt is not null);

    private async Task DownloadAsync(
        Uri address,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(destination);

        var buffer = new byte[81920];
        long received = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;

            if (total > 0)
            {
                progress?.Report((double)received / total.Value);
            }
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(e, "Could not remove {Path}.", path);
        }
    }

    internal sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    internal sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri DownloadUrl);
}
