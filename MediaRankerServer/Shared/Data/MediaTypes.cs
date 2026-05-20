using MediaRankerServer.Shared.Exceptions;

namespace MediaRankerServer.Shared.Data;

public enum MediaType
{
    VideoGame,
    Book,
    Movie,
    TvShow,
    Album,
    Concert,
}

public static class MediaTypes
{
    public static IReadOnlyList<string> All { get; } =
        [nameof(MediaType.VideoGame), nameof(MediaType.Book), nameof(MediaType.Movie),
         nameof(MediaType.TvShow), nameof(MediaType.Album), nameof(MediaType.Concert)];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);

    public static bool IsValid(string? value) =>
        value is not null && AllSet.Contains(value);

    private static bool TryParse(string? value, out MediaType result)
    {
        if (IsValid(value))
        {
            result = Enum.Parse<MediaType>(value!);
            return true;
        }
        result = default;
        return false;
    }

    public static MediaType Parse(string? value) =>
        TryParse(value, out var result)
            ? result
            : throw new DomainException("Media type not found.", "media_type_not_found");
}
