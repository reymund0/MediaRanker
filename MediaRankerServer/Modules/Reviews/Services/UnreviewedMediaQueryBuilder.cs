using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Modules.Reviews.Services;

internal static class UnreviewedMediaQueryBuilder
{
    internal static readonly IReadOnlyCollection<string> SortFields =
        ["title", "releaseDate", "createdAt"];

    internal static readonly IReadOnlyCollection<string> SearchFields =
        ["title"];

    internal static IQueryable<MediaEntity> BaseQuery(
        PostgreSQLContext db, long mediaTypeId, List<long> reviewedMediaIds)
        => db.Media
            .AsNoTracking()
            .Where(m => m.MediaTypeId == mediaTypeId && !reviewedMediaIds.Contains(m.Id))
            .Include(m => m.MediaType)
            .Include(m => m.Cover);

    internal static IQueryable<MediaEntity> ApplySearch(
        IQueryable<MediaEntity> query, PagingValidationResult v)
    {
        if (v.SearchField == "title")
            query = query.Where(m => EF.Functions.ILike(m.Title, v.SearchPattern!, "\\"));
        return query;
    }

    internal static IQueryable<MediaEntity> ApplySort(
        IQueryable<MediaEntity> query, PagingValidationResult v)
        => v.SortField switch
        {
            "releaseDate" => v.Descending
                ? query.OrderBy(m => m.ReleaseDate == null).ThenByDescending(m => m.ReleaseDate).ThenBy(m => m.Id)
                : query.OrderBy(m => m.ReleaseDate == null).ThenBy(m => m.ReleaseDate).ThenBy(m => m.Id),
            "createdAt" => v.Descending
                ? query.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id)
                : query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id),
            _ => v.Descending
                ? query.OrderByDescending(m => m.Title).ThenBy(m => m.Id)
                : query.OrderBy(m => m.Title).ThenBy(m => m.Id),
        };
}
