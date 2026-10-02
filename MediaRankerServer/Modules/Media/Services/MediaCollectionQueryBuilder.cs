using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Media.Services;

internal static class MediaCollectionQueryBuilder
{
    internal static readonly IReadOnlyCollection<string> SortFields =
        ["title", "releaseDate", "createdAt", "updatedAt"];

    internal static readonly IReadOnlyCollection<string> SearchFields =
        ["title"];

    internal static IQueryable<MediaCollection> BaseQuery(PostgreSQLContext db)
        => db.MediaCollections
            .AsNoTracking()
            .Include(mc => mc.ParentMediaCollection)
            .Include(mc => mc.Cover);

    internal static IQueryable<MediaCollection> ApplySearch(
        IQueryable<MediaCollection> query, PagingValidationResult v)
    {
        if (v.SearchField == "title")
            query = query.Where(mc => EF.Functions.ILike(mc.Title, v.SearchPattern!, "\\"));
        return query;
    }

    internal static IQueryable<MediaCollection> ApplyFilters(
        IQueryable<MediaCollection> query, string? mediaType, MediaCollectionType? collectionType, long? parentId)
    {
        if (mediaType is not null) query = query.Where(mc => mc.MediaType == mediaType);
        if (collectionType.HasValue) query = query.Where(mc => mc.CollectionType == collectionType.Value);
        if (parentId.HasValue) query = query.Where(mc => mc.ParentMediaCollectionId == parentId.Value);

        return ApplyVisibility(query);
    }

    internal static IQueryable<MediaCollection> ApplyVisibility(IQueryable<MediaCollection> query)
    {
        query = query.Where(mc => mc.MediaType != "TvShow"
            || mc.CollectionType != MediaCollectionType.Season
            || mc.SeasonNumber.HasValue);
        query = query.Where(mc => mc.MediaType != "TvShow"
            || mc.CollectionType != MediaCollectionType.Series
            || !mc.ChildCollections.Any(child => child.CollectionType == MediaCollectionType.Season)
            || mc.ChildCollections.Any(child => child.CollectionType == MediaCollectionType.Season && child.SeasonNumber.HasValue));
        return query;
    }

    internal static IQueryable<MediaCollection> ApplySort(
        IQueryable<MediaCollection> query, PagingValidationResult v)
        => v.SortField switch
        {
            "releaseDate" => v.Descending
                ? query.OrderBy(c => c.ReleaseDate == null).ThenByDescending(c => c.ReleaseDate).ThenBy(c => c.Id)
                : query.OrderBy(c => c.ReleaseDate == null).ThenBy(c => c.ReleaseDate).ThenBy(c => c.Id),
            "createdAt" => v.Descending
                ? query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
                : query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
            "updatedAt" => v.Descending
                ? query.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
                : query.OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id),
            _ => v.Descending
                ? query.OrderByDescending(c => c.Title).ThenBy(c => c.Id)
                : query.OrderBy(c => c.Title).ThenBy(c => c.Id),
        };

    internal static IQueryable<MediaCollection> ApplySeriesRelevance(
        IQueryable<MediaCollection> query, PagingValidationResult v, bool enabled)
    {
        if (!enabled || v.SearchPattern is not { Length: >= 2 } searchPattern) return ApplySort(query, v);
        var term = searchPattern[1..^1];
        return query
            .OrderByDescending(mc => EF.Functions.ILike(mc.Title, term, "\\"))
            .ThenByDescending(mc => EF.Functions.ILike(mc.Title, $"{term}%", "\\"))
            .ThenBy(mc => mc.Title.Length)
            .ThenBy(mc => mc.Title)
            .ThenBy(mc => mc.Id);
    }
}
