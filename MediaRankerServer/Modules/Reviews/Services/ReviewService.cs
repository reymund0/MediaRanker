using FluentValidation;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Files.Services;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace MediaRankerServer.Modules.Reviews.Services;

public class ReviewService(
  PostgreSQLContext dbContext,
  IValidator<ReviewInsertRequest> reviewInsertRequestValidator,
  IValidator<ReviewUpdateRequest> reviewUpdateRequestValidator,
  IMediaService mediaService,
  ITemplateService templatesService,
  IFileService fileService
  ) : IReviewService
{
    public async Task<List<ReviewDto>> GetReviewsByMediaTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default)
    {
        var userReviews = await dbContext.Reviews
            .AsNoTracking()
            .Include(r => r.Fields)
                .ThenInclude(f => f.TemplateField)
            .Include(r => r.Media)
            .Include(r => r.Template)
            .Include(r => r.Media.MediaType)
            .Where(r => r.UserId == userId && r.Media.MediaTypeId == mediaTypeId)
            .OrderBy(r => r.OverallScore)
            .ToListAsync(cancellationToken);
        return [.. userReviews.Select(r => ReviewDtoMapper.Map(r, fileService))];
    }
    
    public async Task<List<UnreviewedMediaDto>> GetUnreviewedMediaByTypeAsync(string userId, long mediaTypeId, CancellationToken cancellationToken = default)
    {
        var userUnreviewedMedia = await dbContext.Media
            .AsNoTracking()
            .Where(m => m.MediaTypeId == mediaTypeId)
            .Include(m => m.MediaType)
            .Where(m => !m.Reviews.Any(rm => rm.UserId == userId))
            .ToListAsync(cancellationToken);
        return [.. userUnreviewedMedia.Select(m => UnreviewedMediaDtoMapper.Map(m, fileService))];
    }

    public async Task<ReviewDto> CreateReviewAsync(string userId, ReviewInsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateReviewInsertRequestOrThrowAsync(request, cancellationToken);

        // Validate user does not have an existing review for this media.
        var existingReview = await dbContext.Reviews
            .AsNoTracking()
            .Where(rm => rm.UserId == userId && rm.MediaId == request.MediaId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingReview != null)
        {
            throw new DomainException("User already has a review for this media item", "review_insert_duplicate_review");
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
        await dbContext.SaveChangesAsync(cancellationToken);
        
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

        // Validate Media exists
        var media = await mediaService.GetMediaByIdAsync(request.MediaId, cancellationToken) ?? throw new DomainException($"MediaId {request.MediaId} not found", errorType);

        // Validate Template exists
        var template = await templatesService.GetTemplateByIdAsync(request.TemplateId, cancellationToken) ?? throw new DomainException($"TemplateId {request.TemplateId} not found", errorType);
        
        // Validate Template fields exist
        var invalidField = request.Fields.FirstOrDefault(score => !template.Fields.Any(field => field.Id == score.TemplateFieldId));
        if (invalidField is not null)
        {
            throw new DomainException($"Template field {invalidField.TemplateFieldId} not found in template {request.TemplateId}", errorType);
        }

        // Validate Media Type is compatible with Template Media Type
        if (media.MediaType.Id != template.MediaType.Id)
        {
            throw new DomainException($"Media type {media.MediaType.Name} is not compatible with template media type {template.MediaType.Name}", errorType);
        }
    }

    private static short CalculateOverallScore(IEnumerable<double> scores)
    {
        return (short)Math.Round(Enumerable.Average(scores));
    }

    private async Task<ReviewDto?> GetReviewByIdAsync(long reviewId, CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews
            .AsNoTracking()
            .Include(rm => rm.Fields)
                .ThenInclude(f => f.TemplateField)
            .Include(rm => rm.Media)
                .ThenInclude(m => m.MediaType)
            .Include(rm => rm.Template)
            .FirstOrDefaultAsync(rm => rm.Id == reviewId, cancellationToken);
        return review is null ? null : ReviewDtoMapper.Map(review, fileService);
    }
}
