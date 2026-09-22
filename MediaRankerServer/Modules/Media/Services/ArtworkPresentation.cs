using System.Text.RegularExpressions;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;

namespace MediaRankerServer.Modules.Media.Services;

public static partial class ArtworkPresentation
{
    public static CoverPresentation Map(MediaCover cover, DateTimeOffset now)
    {
        if (cover.Outcome == CoverOutcome.Ready && cover.ExpiresAt > now)
        {
            var url = BuildUrl(cover.Provider, cover.ImagePath);
            return url is null ? new(null, "missing") : new(url, "ready");
        }
        if (cover.Outcome == CoverOutcome.Missing && cover.ExpiresAt > now) return new(null, "missing");
        return new(null, cover.Outcome == CoverOutcome.Pending ? "pending" : "failed");
    }

    public static string? BuildUrl(ArtworkProvider provider, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        return provider switch
        {
            ArtworkProvider.Tmdb when TmdbPath().IsMatch(imagePath) => $"https://image.tmdb.org/t/p/w342{imagePath}",
            ArtworkProvider.Igdb when IgdbId().IsMatch(imagePath) => $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imagePath}.jpg",
            _ => null
        };
    }

    [GeneratedRegex(@"^/[a-zA-Z0-9_-]+\.(jpg|png|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex TmdbPath();
    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IgdbId();
}
