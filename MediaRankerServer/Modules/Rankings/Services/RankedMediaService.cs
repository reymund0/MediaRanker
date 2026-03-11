using FluentValidation;
using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Rankings.Entities;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Rankings.Contracts;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace MediaRankerServer.Modules.Rankings.Services;

public class RankedMediaService(
  PostgreSQLContext dbContext,
  IValidator<RankedMediaUpsertRequest> rankedMediaUpsertRequestValidator,
  IMediaService mediaService,
  ITemplatesService templatesService
  ) : IRankedMediaService
{
    public async Task<List<RankedMediaDto>> GetRankedMediaAsync(string userId, CancellationToken cancellationToken = default)
    {
        var userRankedMedia = await dbContext.RankedMedia
            .AsNoTracking()
            .Include(rm => rm.Scores)
            .Include(rm => rm.Media)
            .Where(rm => rm.UserId == userId)
            .ToListAsync(cancellationToken);
        return [.. userRankedMedia.Select(RankedMediaMapper.Map)];
    }

    public async Task<RankedMediaDto> CreateRankedMediaAsync(string userId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateRankedMediaUpsertRequestOrThrowAsync(request, cancellationToken);

        // Normalize strings.
        var normalizedReviewTitle = string.IsNullOrWhiteSpace(request.ReviewTitle) ? null : request.ReviewTitle.Trim();
        var normalizedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Calculate overall score from scores, rounding up.
        var overallScore = CalculateOverallScore(request.Scores.Select(score => (double)score.Value));
        
        // Create RankedMedia entity
        var rankedMedia = new RankedMedia
        {
            UserId = userId,
            MediaId = request.MediaId,
            ReviewTitle = normalizedReviewTitle,
            Notes = normalizedNotes,
            OverallScore = overallScore,
            Scores = [..request.Scores.Select(score => new RankedMediaScore
            {
                TemplateFieldId = score.TemplateFieldId,
                Value = score.Value
            })]
        };

        dbContext.RankedMedia.Add(rankedMedia);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return await GetRankedMediaByIdAsync(rankedMedia.Id, cancellationToken) ?? throw new DomainException("Failed to retrieve newly created Ranked Media", "ranked_media_load_failed");
    }

    public async Task<RankedMediaDto> UpdateRankedMediaAsync(string userId, long rankedMediaId, RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateRankedMediaUpsertRequestOrThrowAsync(request, cancellationToken);

        // Validate ranked media exists and belongs to user
        var rankedMedia = await dbContext.RankedMedia
            .Include(rm => rm.Scores)
            .FirstOrDefaultAsync(rm => rm.Id == rankedMediaId, cancellationToken)
            ?? throw new DomainException("Ranked Media not found", "ranked_media_not_found");
        if (rankedMedia.UserId != userId)
        {
            throw new DomainException("Ranked Media does not belong to user", "ranked_media_forbidden");
        }

        // Normalize fields
        var normalizedReviewTitle = request.ReviewTitle?.Trim();
        var normalizedNotes = request.Notes?.Trim();

        // Recalculate overall score
        var overallScore = CalculateOverallScore(request.Scores.Select(score => (double)score.Value));

        // Update Ranked Media
        rankedMedia.ReviewTitle = normalizedReviewTitle;
        rankedMedia.Notes = normalizedNotes;
        rankedMedia.ConsumedAt = request.ConsumedAt;
        rankedMedia.OverallScore = overallScore;
        rankedMedia.MediaId = request.MediaId;
        rankedMedia.TemplateId = request.TemplateId;

        // Update Ranked Media Scores.
        // Identify new, updated, and removeable scores
        var newScores = request.Scores.Where(score => !rankedMedia.Scores.Any(s => s.TemplateFieldId == score.TemplateFieldId)).ToList();
        var updatedScores = request.Scores.Where(score => rankedMedia.Scores.Any(s => s.TemplateFieldId == score.TemplateFieldId)).ToList();
        var removeableScores = rankedMedia.Scores.Where(score => !request.Scores.Any(s => s.TemplateFieldId == score.TemplateFieldId)).ToList();

        // Add new scores
        foreach (var score in newScores)
        {
            rankedMedia.Scores.Add(new RankedMediaScore
            {
                RankedMediaId = rankedMediaId,
                TemplateFieldId = score.TemplateFieldId,
                Value = score.Value
            });
        }
        
        // Update existing scores
        foreach (var score in updatedScores)
        {
            var existingScore = rankedMedia.Scores.First(s => s.TemplateFieldId == score.TemplateFieldId);
            existingScore.Value = score.Value;
        }
        
        // Remove scores
        dbContext.RankedMediaScores.RemoveRange(removeableScores);
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return await GetRankedMediaByIdAsync(rankedMediaId, cancellationToken) ?? throw new DomainException("Failed to retrieve updated Ranked Media", "ranked_media_load_failed");
    }

    public async Task DeleteRankedMediaAsync(string userId, long rankedMediaId, CancellationToken cancellationToken = default)
    {
        // Validate Ranked Media exists
        var rankedMedia = await dbContext.RankedMedia.FindAsync([rankedMediaId], cancellationToken) ?? throw new DomainException("Ranked Media not found", "ranked_media_not_found");
        
        // Validate user owns Ranked Media
        if (rankedMedia.UserId != userId)
        {
            throw new DomainException("Ranked Media does not belong to user", "ranked_media_forbidden");
        }

        // Delete Ranked Media and its scores.
        dbContext.RankedMediaScores.RemoveRange(dbContext.RankedMediaScores.Where(rms => rms.RankedMediaId == rankedMediaId));
        dbContext.RankedMedia.Remove(rankedMedia);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateRankedMediaUpsertRequestOrThrowAsync(RankedMediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        const string errorType = "ranked_media_upsert_validation_error";

        // Validate request
        await rankedMediaUpsertRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Validate Media exists
        var media = await mediaService.GetMediaByIdAsync(request.MediaId, cancellationToken) ?? throw new DomainException($"MediaId {request.MediaId} not found", errorType);

        // Validate Template exists
        var template = await templatesService.GetTemplateByIdAsync(request.TemplateId, cancellationToken) ?? throw new DomainException($"TemplateId {request.TemplateId} not found", errorType);
        
        // Validate Template fields exist
        var invalidField = request.Scores.FirstOrDefault(score => !template.Fields.Any(field => field.Id == score.TemplateFieldId));
        if (invalidField is not null)
        {
            throw new DomainException($"Template field {invalidField.TemplateFieldId} not found in template {request.TemplateId}", errorType);
        }

        // Validate Media Type is compatible with Template Media Type
        if (media.MediaType != template.MediaType)
        {
            throw new DomainException($"Media type {media.MediaType} is not compatible with template media type {template.MediaType}", errorType);
        }
    }

    private static short CalculateOverallScore(IEnumerable<double> scores)
    {
        return (short)Math.Round(Enumerable.Average(scores));
    }

    private async Task<RankedMediaDto?> GetRankedMediaByIdAsync(long rankedMediaId, CancellationToken cancellationToken = default)
    {
        var rankedMedia = await dbContext.RankedMedia
            .AsNoTracking()
            .Include(rm => rm.Scores)
            .Include(rm => rm.Media)
            .FirstOrDefaultAsync(rm => rm.Id == rankedMediaId, cancellationToken);
        return rankedMedia is null ? null : RankedMediaMapper.Map(rankedMedia);
    }
}
