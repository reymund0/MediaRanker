using FluentAssertions;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ArtworkPresentationTests
{
    [Fact]
    public void Map_FreshTmdbCover_ReturnsTrustedHttpsUrlAndReadyStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var cover = new MediaCover
        {
            Provider = ArtworkProvider.Tmdb,
            LookupKind = CoverLookupKind.MovieImdb,
            LookupId = "tt0133093",
            Outcome = CoverOutcome.Ready,
            ImagePath = "/matrix.jpg",
            ExpiresAt = now.AddDays(1)
        };

        var presentation = ArtworkPresentation.Map(cover, now);

        presentation.Status.Should().Be("ready");
        presentation.Url.Should().Be("https://image.tmdb.org/t/p/w342/matrix.jpg");
    }

    [Fact]
    public void Map_ExpiredCover_WithholdsStaleUrl()
    {
        var now = DateTimeOffset.UtcNow;
        var cover = new MediaCover
        {
            Provider = ArtworkProvider.Igdb,
            LookupKind = CoverLookupKind.IgdbGame,
            LookupId = "42",
            Outcome = CoverOutcome.Ready,
            ImagePath = "co4k2o",
            ExpiresAt = now.AddTicks(-1)
        };

        var presentation = ArtworkPresentation.Map(cover, now);

        presentation.Url.Should().BeNull();
        presentation.Status.Should().NotBe("ready");
    }

    [Fact]
    public void Map_InvalidProviderAsset_ReturnsPlaceholderWithoutExposingArbitraryUrl()
    {
        var now = DateTimeOffset.UtcNow;
        var cover = new MediaCover
        {
            Provider = ArtworkProvider.Tmdb,
            LookupKind = CoverLookupKind.MovieImdb,
            LookupId = "tt0133093",
            Outcome = CoverOutcome.Ready,
            ImagePath = "https://untrusted.example/cover.jpg",
            ExpiresAt = now.AddDays(1)
        };

        var presentation = ArtworkPresentation.Map(cover, now);

        presentation.Url.Should().BeNull();
        presentation.Status.Should().Be("missing");
    }
}
