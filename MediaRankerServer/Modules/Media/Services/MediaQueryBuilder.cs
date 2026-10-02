using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

internal static class MediaQueryBuilder
{
    internal static readonly IReadOnlyCollection<string> SortFields =
        ["title", "releaseDate", "createdAt", "updatedAt", "episodeNumber"];

    internal static readonly IReadOnlyCollection<string> SearchFields =
        ["title"];

    internal static IQueryable<MediaEntity> BaseQuery(PostgreSQLContext db)
        => db.Media
            .AsNoTracking()
            .Include(m => m.MediaCollection)!
            .ThenInclude(collection => collection!.ParentMediaCollection)
            .Include(m => m.Cover);

    internal static IQueryable<MediaEntity> ApplySearch(
        IQueryable<MediaEntity> query, PagingValidationResult v)
    {
        if (v.SearchField == "title")
            query = query.Where(m => EF.Functions.ILike(m.Title, v.SearchPattern!, "\\"));
        return query;
    }

    internal static IQueryable<MediaEntity> ApplyVisibility(IQueryable<MediaEntity> query)
        => query.Where(m => m.MediaType != "TvShow" || m.MediaCollection == null
            || m.MediaCollection.CollectionType != MediaCollectionType.Season
            || m.MediaCollection.SeasonNumber.HasValue);

    internal static IQueryable<MediaEntity> ApplySort(
        IQueryable<MediaEntity> query, PagingValidationResult v)
    {
        if (v.SortField == "episodeNumber")
            return query.OrderBy(m => m.EpisodeNumber == null)
                .ThenBy(m => m.EpisodeNumber).ThenBy(m => m.Title).ThenBy(m => m.Id);

        if (v.SearchPattern is { Length: >= 2 } searchPattern)
        {
            // SearchPattern contains the escaped term wrapped in '%' wildcards.
            // The clone TV catalog made the full exact/prefix/length sort spill to disk;
            // this prefix-only relevance keeps the prefix grouping and a stable title order.
            var searchTerm = searchPattern[1..^1];
            return query
                .OrderByDescending(m => EF.Functions.ILike(m.Title, $"{searchTerm}%", "\\"))
                .ThenBy(m => m.Title)
                .ThenBy(m => m.Id);
        }

        return v.SortField switch
        {
            "releaseDate" => v.Descending
                ? query.OrderBy(m => m.ReleaseDate == null).ThenByDescending(m => m.ReleaseDate).ThenBy(m => m.Id)
                : query.OrderBy(m => m.ReleaseDate == null).ThenBy(m => m.ReleaseDate).ThenBy(m => m.Id),
            "createdAt" => v.Descending
                ? query.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id)
                : query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id),
            "updatedAt" => v.Descending
                ? query.OrderByDescending(m => m.UpdatedAt).ThenBy(m => m.Id)
                : query.OrderBy(m => m.UpdatedAt).ThenBy(m => m.Id),
            _ => v.Descending
                ? query.OrderByDescending(m => m.Title).ThenBy(m => m.Id)
                : query.OrderBy(m => m.Title).ThenBy(m => m.Id),
        };
    }
}
