using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Titanite.Abstractions.Hosting;
using Titanite.Abstractions.Processes;
using Titanite.Abstractions.Updates;
using Titanite.Platform.Updates;
using static Titanite.Platform.Updates.GitHubReleaseUpdater;

namespace Titanite.Platform.Tests.Updates;

public sealed class GitHubReleaseUpdaterTests : IDisposable
{
    private static readonly Uri Repository = new("https://github.com/example/titanite");

    private readonly IApplicationInfo _application = A.Fake<IApplicationInfo>();

    private readonly IHostProcesses _host = A.Fake<IHostProcesses>();

    private readonly FakeHandler _handler = new();

    private readonly string _downloads = Path.Combine(Path.GetTempPath(), $"titanite-updates-{Guid.NewGuid():N}");

    public GitHubReleaseUpdaterTests()
    {
        A.CallTo(() => _application.Version).Returns("0.1.0");
        A.CallTo(() => _application.RepositoryUrl).Returns(Repository);
    }

    [Fact]
    public void AsksGitHubForTheRepositoryReleases() =>
        Assert.Equal(
            "https://api.github.com/repos/example/titanite/releases?per_page=30",
            ReleasesAddress(Repository).AbsoluteUri);

    [Fact]
    public void OffersTheMostRecentlyPublishedRelease()
    {
        var update = SelectUpdate(
            "0.1.0",
            [Release("v0.1.0", day: 1), Release("v0.3.0", day: 3), Release("v0.2.0", day: 2)],
            Architecture.X64);

        Assert.Equal("0.3.0", update?.Version);
        Assert.Equal("Titanite-0.3.0-x86_64.flatpak", update?.BundleName);
    }

    [Fact]
    public void GoesByPublishDateRatherThanVersion() =>
        Assert.Equal(
            "0.1.1",
            SelectUpdate("0.2.0", [Release("v0.2.0", day: 1), Release("v0.1.1", day: 2)], Architecture.X64)?.Version);

    [Fact]
    public void IncludesPrereleases() =>
        Assert.Equal(
            "0.2.0-beta",
            SelectUpdate("0.1.0", [Release("v0.1.0", day: 1), Release("v0.2.0-beta", day: 2)], Architecture.X64)?.Version);

    [Fact]
    public void OffersNothingWhenRunningTheMostRecentRelease() =>
        Assert.Null(SelectUpdate("0.3.0", [Release("v0.2.0", day: 1), Release("v0.3.0", day: 2)], Architecture.X64));

    [Fact]
    public void OffersTheLatestReleaseWhenNoReleaseIsTaggedForThisVersion() =>
        Assert.Equal(
            "0.2.0",
            SelectUpdate("0.1.0-local", [Release("v0.1.0", day: 1), Release("v0.2.0", day: 2)], Architecture.X64)?.Version);

    [Fact]
    public void OffersNothingWhenThereAreNoReleases() =>
        Assert.Null(SelectUpdate("0.1.0", [], Architecture.X64));

    [Fact]
    public void FindsTheReleaseTaggedForThisVersion() =>
        Assert.Equal(
            "v0.1.0-alpha",
            FindRunningRelease("0.1.0-alpha", [Release("v0.1.0", day: 2), Release("v0.1.0-alpha", day: 1)])?.TagName);

    [Fact]
    public void IgnoresDrafts() =>
        Assert.Null(SelectUpdate(
            "0.1.0",
            [Release("v0.1.0", day: 1), Release("v0.2.0", day: 2) with { Draft = true, PublishedAt = null }],
            Architecture.X64));

    [Fact]
    public void OffersNothingWhenTheNewestReleaseHasNoBundle() =>
        Assert.Null(SelectUpdate(
            "0.1.0",
            [Release("v0.1.0", day: 1), Release("v0.2.0", day: 2) with { Assets = [] }],
            Architecture.X64));

    [Fact]
    public void OffersOnlyABundleForThisMachine() =>
        Assert.Null(SelectUpdate("0.1.0", [Release("v0.1.0", day: 1), Release("v0.2.0", day: 2)], Architecture.Arm64));

    [Fact]
    public async Task DoesNotCheckInDevelopment()
    {
        A.CallTo(() => _application.IsDevelopment).Returns(true);

        var updater = Updater();

        Assert.Null(await updater.CheckAsync());
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task OffersTheUpdateGitHubLists()
    {
        _handler.Respond(
            ReleasesAddress(Repository),
            """
            [{
              "tag_name": "v0.2.0",
              "draft": false,
              "prerelease": true,
              "published_at": "2026-09-23T10:00:00Z",
              "assets": [{
                "name": "Titanite-0.2.0-x86_64.flatpak",
                "browser_download_url": "https://github.com/example/titanite/releases/download/v0.2.0/Titanite-0.2.0-x86_64.flatpak"
              }]
            },
            {
              "tag_name": "v0.1.0",
              "draft": false,
              "prerelease": false,
              "published_at": "2026-09-01T10:00:00Z",
              "assets": []
            }]
            """u8.ToArray());

        var updater = Updater();

        var update = await updater.CheckAsync();

        Assert.Equal("0.2.0", update?.Version);
        Assert.Equal(ReleasesAddress(Repository), Assert.Single(_handler.Requests).RequestUri);
    }

    [Fact]
    public async Task OffersNothingWhenGitHubCannotBeReached()
    {
        var updater = Updater();

        Assert.Null(await updater.CheckAsync());
    }

    [Fact]
    public async Task InstallsTheDownloadedBundleForTheUser()
    {
        var update = new AppUpdate("0.2.0", "Titanite-0.2.0-x86_64.flatpak", new Uri("https://example.com/bundle"));
        var bundle = Path.Combine(_downloads, update.BundleName);
        string? downloaded = null;

        _handler.Respond(update.BundleUrl, Encoding.ASCII.GetBytes("bundle"));

        A.CallTo(() => _host.RunAsync("flatpak", A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                downloaded = File.ReadAllText(bundle);

                return true;
            });

        var updater = Updater();
        var progress = new List<double>();

        Assert.True(await updater.InstallAsync(update, new SynchronousProgress(progress.Add)));

        A.CallTo(() => _host.RunAsync(
                "flatpak",
                A<IReadOnlyList<string>>.That.IsSameSequenceAs("install", "--user", "--noninteractive", "--reinstall", bundle),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.Equal("bundle", downloaded);
        Assert.Equal(1.0, progress[^1]);
        Assert.False(File.Exists(bundle));
    }

    [Fact]
    public async Task ReportsADownloadThatFailed()
    {
        var update = new AppUpdate("0.2.0", "Titanite-0.2.0-x86_64.flatpak", new Uri("https://example.com/missing"));

        var updater = Updater();

        Assert.False(await updater.InstallAsync(update));
        A.CallTo(() => _host.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ReportsAnInstallThatFailed()
    {
        var update = new AppUpdate("0.2.0", "Titanite-0.2.0-x86_64.flatpak", new Uri("https://example.com/bundle"));

        _handler.Respond(update.BundleUrl, [1, 2, 3]);
        A.CallTo(() => _host.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(false);

        var updater = Updater();

        Assert.False(await updater.InstallAsync(update));
    }

    public void Dispose()
    {
        if (Directory.Exists(_downloads))
        {
            Directory.Delete(_downloads, recursive: true);
        }
    }

    private GitHubReleaseUpdater Updater() =>
        new(
            new HttpClient(_handler),
            _application,
            _host,
            Options.Create(new UpdaterOptions { DownloadDirectory = _downloads }),
            NullLogger<GitHubReleaseUpdater>.Instance);

    private static GitHubRelease Release(string tag, int day)
    {
        var version = tag.TrimStart('v');
        var name = $"Titanite-{version}-x86_64.flatpak";

        return new GitHubRelease(
            tag,
            Draft: false,
            new DateTimeOffset(2026, 9, day, 12, 0, 0, TimeSpan.Zero),
            [
                new GitHubAsset("Source.tar.gz", new Uri($"https://github.com/example/titanite/archive/{tag}.tar.gz")),
                new GitHubAsset(name, new Uri($"https://github.com/example/titanite/releases/download/{tag}/{name}"))
            ]);
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<Uri, byte[]> _responses = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Respond(Uri address, byte[] content) => _responses[address] = content;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(_responses.TryGetValue(request.RequestUri!, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
