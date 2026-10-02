using FluentValidation;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.Data.Views;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;

using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace MediaRankerServer.Modules.Reviews.Services;

public class ReviewService(
  PostgreSQLContext dbContext,
  IValidator<ReviewInsertRequest> reviewInsertRequestValidator,
  IValidator<ReviewUpdateRequest> reviewUpdateRequestValidator,
  IMediaService mediaService,
  ITemplateService templatesService,
  IArtworkService artworkService
  ) : IReviewService
{
    public async Task<List<ReviewDto>> GetReviewsByMediaTypeAsync(string userId, string mediaType, CancellationToken cancellationToken = default)
    {
        MediaTypes.Parse(mediaType);

        var reviewDetails = await dbContext.ReviewDetails
            .AsNoTracking()
            .Where(r => r.MediaType == mediaType && r.UserId == userId)
            .OrderByDescending(r => r.OverallScore)
            .ThenByDescending(r => r.UpdatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        if (reviewDetails.Count == 0) return [];

        var reviewIds = reviewDetails.Select(r => r.Id).ToList();

        // Load template field info for the review fields.
        var fields = await (
            from rf in dbContext.ReviewFields
            join tf in dbContext.TemplateFields on rf.TemplateFieldId equals tf.Id
            where reviewIds.Contains(rf.ReviewId)
            select new ReviewDtoMapper.ReviewFieldDetails(rf, tf.Name, tf.Position)
        ).ToListAsync(cancellationToken);

        var mediaIds = reviewDetails
            .Where(r => r.MediaId.HasValue)
            .Select(r => r.MediaId!.Value)
            .Distinct()
            .ToArray();
        var mediaReleaseDates = await dbContext.Media
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.ReleaseDate, cancellationToken);

        var seriesIds = reviewDetails
            .Where(r => r.ReviewKind is "Series" or "Episode")
            .Where(r => r.SeriesId.HasValue)
            .Select(r => r.SeriesId!.Value)
            .Distinct()
            .ToArray();
        var reviewedSeriesIds = reviewDetails
            .Where(r => r.ReviewKind == "Series" && r.MediaCollectionId.HasValue)
            .Select(r => r.MediaCollectionId!.Value)
            .Distinct()
            .ToArray();
        var collectionCovers = await artworkService.GetCollectionArtworkAsync(seriesIds, cancellationToken);
        var mediaCovers = await artworkService.GetMediaArtworkAsync(mediaIds, cancellationToken);
        var collectionReleaseDates = await dbContext.MediaCollections.AsNoTracking()
            .Where(collection => reviewedSeriesIds.Contains(collection.Id))
            .ToDictionaryAsync(collection => collection.Id, collection => collection.ReleaseDate, cancellationToken);
        return [.. reviewDetails.Select(r => ReviewDtoMapper.Map(
            GetCover(r, mediaCovers, collectionCovers),
            r,
            fields.Where(f => f.Field.ReviewId == r.Id),
            r.MediaId is { } mediaId ? mediaReleaseDates.GetValueOrDefault(mediaId)
                : r.MediaCollectionId is { } collectionId ? collectionReleaseDates.GetValueOrDefault(collectionId) : null))];
    }
    
    public async Task<PageResult<UnreviewedMediaDto>> GetUnreviewedMediaByTypeAsync(string userId, string mediaType, PageRequest request, CancellationToken cancellationToken = default)
    {
        MediaTypes.Parse(mediaType);

        var reviewedMediaIds = await dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.MediaId)
            .Where(mediaId => mediaId.HasValue)
            .Select(mediaId => mediaId!.Value)
            .ToListAsync(cancellationToken);

        var v = PagingValidator.Validate(request, UnreviewedMediaQueryBuilder.SortFields, UnreviewedMediaQueryBuilder.SearchFields, "title");

        var query = UnreviewedMediaQueryBuilder.ApplySearch(
            UnreviewedMediaQueryBuilder.BaseQuery(dbContext, mediaType, reviewedMediaIds), v);
        int? totalCount = null;
        if (request.IncludeTotalCount == true)
            totalCount = await query.CountAsync(cancellationToken);
        query = UnreviewedMediaQueryBuilder.ApplySort(query, v);

        var page = await query.Skip(v.Skip).Take(v.Take).ToListAsync(cancellationToken);
        var covers = await artworkService.GetMediaArtworkAsync(page.Select(m => m.Id), cancellationToken);

        return new PageResult<UnreviewedMediaDto>(
            [.. page.Select(m => UnreviewedMediaDtoMapper.Map(m, covers?.GetValueOrDefault(m.Id)))],
            totalCount, v.Page, v.PageSize);
    }

    public async Task<ReviewDto> CreateReviewAsync(string userId, ReviewInsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateReviewInsertRequestOrThrowAsync(request, cancellationToken);

        // Validate user does not have an existing review for this media.
        var alreadyReviewed = request.MediaId is { } mediaId
            ? await dbContext.Reviews.AsNoTracking().AnyAsync(r => r.UserId == userId && r.MediaId == mediaId, cancellationToken)
            : await dbContext.Reviews.AsNoTracking().AnyAsync(r => r.UserId == userId && r.MediaCollectionId == request.MediaCollectionId, cancellationToken);
        if (alreadyReviewed)
        {
            throw new DomainException("User already has a review for this item", "review_insert_duplicate_review");
        }

        // Normalize strings.
        var normalizedReviewTitle = string.IsNullOrWhiteSpace(request.ReviewTitle) ? null : request.ReviewTitle.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Calculate overall score from scores, rounding up.
        var overallScore = CalculateOverallScore(request.Fields.Select(score => (double)score.Value));
        
        // Create Reviews entity
        var review = new Review
        {
            UserId = userId,
            MediaId = request.MediaId,
            MediaCollectionId = request.MediaCollectionId,
            TemplateId = request.TemplateId,
            ReviewTitle = normalizedReviewTitle,
            Notes = normalizedNotes,
            OverallScore = overallScore,
            Fields = [..request.Fields.Select(score => new ReviewField
            {
                TemplateFieldId = score.TemplateFieldId,
                Value = score.Value
            })]
        };

        dbContext.Reviews.Add(review);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsReviewTargetUniqueViolation(exception))
        {
            throw new DomainException("User already has a review for this item", "review_insert_duplicate_review");
        }
        
        return await GetReviewByIdAsync(review.Id, cancellationToken) ?? throw new DomainException("Failed to retrieve newly created Review", "reviews_load_failed");
    }

    public async Task<ReviewDto> UpdateReviewAsync(string userId, long reviewId, ReviewUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = reviewUpdateRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors.First().ErrorMessage, "review_update_validation_error");
        }

        // Validate review exists and belongs to user
        var review = await dbContext.Reviews
            .Include(rm => rm.Fields)
            .FirstOrDefaultAsync(rm => rm.Id == reviewId, cancellationToken)
            ?? throw new DomainException("Review not found", "review_not_found");
        if (review.UserId != userId)
        {
            throw new DomainException("Review does not belong to user", "review_forbidden");
        }

        // Normalize fields
        var normalizedReviewTitle = request.ReviewTitle?.Trim();
        var normalizedNotes = request.Notes?.Trim();

        // Recalculate overall score
        var overallScore = CalculateOverallScore(request.Fields.Select(score => (double)score.Value));

        // Update Review
        review.ReviewTitle = normalizedReviewTitle;
        review.Notes = normalizedNotes;
        review.ConsumedAt = request.ConsumedAt;
        review.OverallScore = overallScore;

        // Identify new, and updated Review fields.
        // We don't have any to remove because we are handling that with the TemplateFieldsDeleted event handler in the edge case a template is updated.
        var newScores = request.Fields.Where(score => !review.Fields.Any(s => s.TemplateFieldId == score.TemplateFieldId)).ToList();
        var updatedScores = request.Fields.Where(score => review.Fields.Any(s => s.TemplateFieldId == score.TemplateFieldId)).ToList();

        // Add new scores
        foreach (var score in newScores)
        {
            review.Fields.Add(new ReviewField
            {
                ReviewId = reviewId,
                TemplateFieldId = score.TemplateFieldId,
                Value = score.Value
            });
        }
        
        // Update existing scores
        foreach (var score in updatedScores)
        {
            var existingScore = review.Fields.First(s => s.TemplateFieldId == score.TemplateFieldId);
            existingScore.Value = score.Value;
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return await GetReviewByIdAsync(reviewId, cancellationToken) ?? throw new DomainException("Failed to retrieve updated Review", "reviews_load_failed");
    }

    public async Task DeleteReviewAsync(string userId, long reviewId, CancellationToken cancellationToken = default)
    {
        // Validate Review exists
        var review = await dbContext.Reviews.FindAsync([reviewId], cancellationToken) ?? throw new DomainException("Review not found", "review_not_found");
        
        // Validate user owns Review
        if (review.UserId != userId)
        {
            throw new DomainException("Review does not belong to user", "review_forbidden");
        }

        // Delete Review and its scores.
        dbContext.ReviewFields.RemoveRange(dbContext.ReviewFields.Where(rms => rms.ReviewId == reviewId));
        dbContext.Reviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateReviewInsertRequestOrThrowAsync(ReviewInsertRequest request, CancellationToken cancellationToken = default)
    {
        const string errorType = "review_insert_validation_error";

        // Validate request
        var validationResult = reviewInsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, errorType);
        }

        MediaDto? media = null;
        if (request.MediaId is { } mediaId)
        {
            media = await mediaService.GetMediaByIdAsync(mediaId, cancellationToken, requestArtwork: false)
                ?? throw new DomainException($"MediaId {mediaId} not found", errorType);
        }
        else if (request.MediaCollectionId is { } collectionId)
        {
            var collection = await dbContext.MediaCollections.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == collectionId, cancellationToken);
            if (collection is null)
                throw new DomainException($"MediaCollectionId {collectionId} not found", errorType);
            if (collection.MediaType != "TvShow" || collection.CollectionType != MediaCollectionType.Series)
                throw new DomainException("Reviews can only target TV series collections.", errorType);
            var seasons = dbContext.MediaCollections.Where(item =>
                item.ParentMediaCollectionId == collectionId && item.CollectionType == MediaCollectionType.Season);
            if (await seasons.AnyAsync(cancellationToken)
                && !await seasons.AnyAsync(item => item.SeasonNumber.HasValue, cancellationToken))
                throw new DomainException($"MediaCollectionId {collectionId} not found", errorType);
        }

        // Validate Template exists
        var template = await templatesService.GetTemplateByIdAsync(request.TemplateId, cancellationToken) ?? throw new DomainException($"TemplateId {request.TemplateId} not found", errorType);
        
        // Validate Template fields exist
        var invalidField = request.Fields.FirstOrDefault(score => !template.Fields.Any(field => field.Id == score.TemplateFieldId));
        if (invalidField is not null)
        {
            throw new DomainException($"Template field {invalidField.TemplateFieldId} not found in template {request.TemplateId}", errorType);
        }

        // Validate media type
        if (media is not null && media.MediaType != template.MediaType)
        {
            throw new DomainException("Media type does not match template media type.", "review_media_type_mismatch");
        }
        if (request.MediaCollectionId.HasValue && template.MediaType != "TvShow")
            throw new DomainException("TV series reviews require a TV template.", "review_media_type_mismatch");
    }

    private static short CalculateOverallScore(IEnumerable<double> scores)
    {
        return (short)Math.Round(Enumerable.Average(scores));
    }

    private async Task<ReviewDto?> GetReviewByIdAsync(long reviewId, CancellationToken cancellationToken = default)
    {
        var review = await dbContext.ReviewDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

        if (review == null) return null;

        var fields = await (
            from rf in dbContext.ReviewFields
            join tf in dbContext.TemplateFields on rf.TemplateFieldId equals tf.Id
            where rf.ReviewId == reviewId
            select new ReviewDtoMapper.ReviewFieldDetails(rf, tf.Name, tf.Position)
        ).ToListAsync(cancellationToken);

        var mediaReleaseDate = review.MediaId is { } mediaId
            ? await dbContext.Media
                .AsNoTracking()
                .Where(m => m.Id == mediaId)
                .Select(m => m.ReleaseDate)
                .FirstOrDefaultAsync(cancellationToken)
            : review.MediaCollectionId is { } collectionId
                ? await dbContext.MediaCollections.AsNoTracking()
                    .Where(collection => collection.Id == collectionId)
                    .Select(collection => collection.ReleaseDate)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

        var mediaCovers = review.MediaId is { } mediaIdForCover
            ? await artworkService.GetMediaArtworkAsync([mediaIdForCover], cancellationToken)
            : null;
        var collectionCovers = review.SeriesId is { } seriesId
            && review.ReviewKind is "Series" or "Episode"
            ? await artworkService.GetCollectionArtworkAsync([seriesId], cancellationToken)
            : null;
        return ReviewDtoMapper.Map(
            GetCover(review, mediaCovers, collectionCovers),
            review,
            fields,
            mediaReleaseDate);
    }

    private static CoverPresentation? GetCover(
        ReviewDetailView review,
        IReadOnlyDictionary<long, CoverPresentation>? mediaCovers,
        IReadOnlyDictionary<long, CoverPresentation>? collectionCovers)
    {
        if (review.ReviewKind is "Series" or "Episode" && review.SeriesId is { } seriesId)
            return collectionCovers?.GetValueOrDefault(seriesId);
        return review.MediaId is { } mediaId ? mediaCovers?.GetValueOrDefault(mediaId) : null;
    }

    private static bool IsReviewTargetUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_reviews_user_media" or "uq_reviews_user_media_collection"
        };
}
