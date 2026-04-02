using MediatR;
using MediaRankerServer.Modules.Templates.Events;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Modules.Reviews.EventHandlers;

public class TemplateFieldsDeletedHandler(
    PostgreSQLContext dbContext,
    ILogger<TemplateFieldsDeletedHandler> logger
) : INotificationHandler<TemplateFieldsDeletedEvent>
{
    public async Task Handle(TemplateFieldsDeletedEvent notification, CancellationToken cancellationToken)
    {
        var fieldIds = notification.FieldIds;

        // Find affected review IDs before deleting fields.
        var affectedReviewIds = await dbContext.ReviewFields
            .Where(rf => fieldIds.Contains(rf.TemplateFieldId))
            .Select(rf => rf.ReviewId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (affectedReviewIds.Count == 0)
        {
            logger.LogInformation(
                "TemplateFieldsDeletedEvent for Template {TemplateId}: no affected reviews.",
                notification.TemplateId);
            return;
        }

        // Delete the review fields for the removed template fields.
        var fieldsToRemove = await dbContext.ReviewFields
            .Where(rf => fieldIds.Contains(rf.TemplateFieldId))
            .ToListAsync(cancellationToken);

        dbContext.ReviewFields.RemoveRange(fieldsToRemove);
        await dbContext.SaveChangesAsync(cancellationToken);

        // For each affected review, recalculate score or delete if no fields remain.
        var remainingByReview = await dbContext.ReviewFields
            .Where(rf => affectedReviewIds.Contains(rf.ReviewId))
            .GroupBy(rf => rf.ReviewId)
            .Select(g => new { ReviewId = g.Key, Average = g.Average(rf => (double)rf.Value) })
            .ToListAsync(cancellationToken);

        var remainingReviewIds = remainingByReview.Select(r => r.ReviewId).ToHashSet();
        var reviewIdsToDelete = affectedReviewIds.Where(id => !remainingReviewIds.Contains(id)).ToList();

        // Delete reviews with no remaining fields.
        var deletedReviewCount = 0;
        if (reviewIdsToDelete.Count > 0)
        {
            var reviewsToDelete = await dbContext.Reviews
                .Where(r => reviewIdsToDelete.Contains(r.Id))
                .ToListAsync(cancellationToken);
            dbContext.Reviews.RemoveRange(reviewsToDelete);
            await dbContext.SaveChangesAsync(cancellationToken);
            deletedReviewCount = reviewsToDelete.Count;
        }

        // Recalculate OverallScore for reviews that still have fields.
        var recalculatedCount = 0;
        if (remainingByReview.Count > 0)
        {
            var reviewsToUpdate = await dbContext.Reviews
                .Where(r => remainingReviewIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            var scoreByReview = remainingByReview.ToDictionary(g => g.ReviewId, g => (short)Math.Round(g.Average));
            foreach (var review in reviewsToUpdate)
            {
                review.OverallScore = scoreByReview[review.Id];
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            recalculatedCount = reviewsToUpdate.Count;
        }

        logger.LogInformation(
            "TemplateFieldsDeletedEvent for Template {TemplateId}: deleted {FieldCount} review field(s), recalculated {RecalcCount} review(s), deleted {ReviewCount} review(s) with no remaining fields.",
            notification.TemplateId,
            fieldsToRemove.Count,
            recalculatedCount,
            deletedReviewCount);
    }
}
