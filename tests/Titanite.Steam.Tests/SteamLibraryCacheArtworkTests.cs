using Titanite.Core.Games;
using Titanite.Steam.Artwork;
using Titanite.Steam.Library;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public sealed class SteamLibraryCacheArtworkTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("titanite-librarycache-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteArtwork(uint appId, string name, string? hash = null)
    {
        var directory = hash is null
            ? Path.Combine(_root, "appcache", "librarycache", appId.ToString())
            : Path.Combine(_root, "appcache", "librarycache", appId.ToString(), hash);

        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "not really a jpeg");

        return path;
    }

    private SteamLibraryCacheArtworkService CreateService(string? root = null) =>
        new(FakeSteamInstall.At(root ?? _root));

    private static async Task<string?> SourceFor(
        SteamLibraryCacheArtworkService service,
        uint appId,
        GameArtworkKind kind = GameArtworkKind.Capsule) =>
        await service.GetArtworkSourceAsync(appId, kind);

    [Fact]
    public void FindsArtworkSittingDirectlyInTheAppsDirectory()
    {
        var expected = WriteArtwork(2138720, "library_600x900.jpg");

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 2138720, GameArtworkKind.Capsule));
    }

    [Fact]
    public void FindsArtworkInsideAHashedDirectory()
    {
        var expected = WriteArtwork(3751950, "library_capsule.jpg", "36a1644b03afce1a648ab90b232196609e827539");

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 3751950, GameArtworkKind.Capsule));
    }

    [Theory]
    [InlineData("library_600x900.jpg")]
    [InlineData("library_capsule.jpg")]
    public void AcceptsEitherNameForTheCover(string name)
    {
        var expected = WriteArtwork(440, name);

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 440, GameArtworkKind.Capsule));
    }

    [Theory]
    [InlineData("header.jpg")]
    [InlineData("library_header.jpg")]
    public void AcceptsEitherNameForTheBanner(string name)
    {
        var expected = WriteArtwork(440, name);

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 440, GameArtworkKind.Header));
    }

    [Fact]
    public void DoesNotOfferOneShapeWhenAskedForTheOther()
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(SteamLibraryCache.Find(_root, 440, GameArtworkKind.Header));
    }

    [Fact]
    public void FindsNothingForAnAppSteamHasNotCached() =>
        Assert.Null(SteamLibraryCache.Find(_root, 1493710, GameArtworkKind.Capsule));

    [Fact]
    public void DoesNotReachIntoAnotherAppsDirectory()
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(SteamLibraryCache.Find(_root, 4400, GameArtworkKind.Capsule));
    }

    [Fact]
    public async Task ServesACachedCoverOverTheArtworkScheme()
    {
        WriteArtwork(3751950, "library_capsule.jpg", "36a1644b");

        Assert.Equal("artwork://steam/3751950/capsule", await SourceFor(CreateService(), 3751950));
    }

    [Fact]
    public async Task OffersNothingWhenSteamHasNotCachedTheApp() =>
        Assert.Null(await SourceFor(CreateService(), 1493710));

    [Fact]
    public async Task OffersNothingWhenSteamIsNotInstalled() =>
        Assert.Null(await SourceFor(new SteamLibraryCacheArtworkService(FakeSteamInstall.At(null)), 440));

    [Fact]
    public void OpensTheFileTheUrlStandsFor()
    {
        WriteArtwork(440, "library_600x900.jpg");

        var content = CreateService().Open("artwork://steam/440/capsule");

        Assert.NotNull(content);
        Assert.Equal("image/jpeg", content.ContentType);

        using var reader = new StreamReader(content.Content);
        Assert.Equal("not really a jpeg", reader.ReadToEnd());
    }

    [Theory]
    [InlineData("artwork://steam/440")]
    [InlineData("artwork://steam/440/hero")]
    [InlineData("artwork://steam/not-a-number/capsule")]
    [InlineData("https://example.invalid/440/capsule")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void DeclinesAUrlItDidNotWrite(string? url)
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(CreateService().Open(url));
    }

    [Fact]
    public async Task NoticesArtworkThatArrivesWhileRunning()
    {
        var service = CreateService();

        Assert.Null(await SourceFor(service, 440));

        WriteArtwork(440, "library_600x900.jpg");

        Assert.Equal("artwork://steam/440/capsule", await SourceFor(service, 440));
    }

    [Fact]
    public void FollowsArtworkThatSteamHasMoved()
    {
        var path = WriteArtwork(440, "library_600x900.jpg", "old");
        var service = CreateService();

        service.Open("artwork://steam/440/capsule")?.Content.Dispose();

        File.Delete(path);
        WriteArtwork(440, "library_600x900.jpg", "new");

        var content = service.Open("artwork://steam/440/capsule");

        Assert.NotNull(content);
        content.Content.Dispose();
    }

    [Fact]
    public void ForgetsAFileThatHasGoneSinceItWasFound()
    {
        var path = WriteArtwork(440, "library_600x900.jpg");
        var service = CreateService();
        var first = service.Open("artwork://steam/440/capsule");

        Assert.NotNull(first);
        first.Content.Dispose();

        File.Delete(path);

        Assert.Null(service.Open("artwork://steam/440/capsule"));
    }
}
