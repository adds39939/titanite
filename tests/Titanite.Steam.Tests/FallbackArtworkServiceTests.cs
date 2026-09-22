using Titanite.Core.Games;
using Titanite.Steam.Artwork;
using Titanite.Steam.Vdf;

namespace Titanite.Steam.Tests;

public class FallbackArtworkServiceTests
{
    private static Task<string?> SourceFor(params IArtworkSource[] sources) =>
        new FallbackArtworkService(sources).GetArtworkSourceAsync(
            SteamIds.For(440),
            GameArtworkKind.Capsule);

    private static IArtworkSource Offering(string? source)
    {
        var provider = A.Fake<IArtworkSource>();

        A.CallTo(() => provider.GetArtworkSourceAsync(A<uint>._, A<GameArtworkKind>._, A<CancellationToken>._))
            .Returns(source);

        return provider;
    }

    [Fact]
    public async Task TakesTheFirstProviderThatOffersSomething() =>
        Assert.Equal("first", await SourceFor(Offering("first"), Offering("second")));

    [Fact]
    public async Task FallsThroughToTheNextWhenOneHasNothing() =>
        Assert.Equal("second", await SourceFor(Offering(null), Offering("second")));

    [Fact]
    public async Task OffersNothingWhenNoProviderCan() =>
        Assert.Null(await SourceFor(Offering(null), Offering(null)));

    [Fact]
    public async Task OffersNothingWhenThereAreNoProviders() =>
        Assert.Null(await SourceFor());

    [Fact]
    public async Task LeavesTheCdnBehindTheLocalCache()
    {
        var source = await SourceFor(Offering(null), new SteamCdnArtworkService());

        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/440/library_600x900.jpg", source);
    }
}
