using MediatR;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Modules.Reviews.EventHandlers;

public sealed class SeriesDeletedHandler(
    PostgreSQLContext dbContext,
    ILogger<SeriesDeletedHandler> logger
) : INotificationHandler<SeriesDeletedEvent>
{
    public async Task Handle(SeriesDeletedEvent notification, CancellationToken cancellationToken)
    {
        var reviews = await dbContext.Reviews
            .Where(review => review.MediaCollectionId == notification.SeriesId
                || review.MediaId.HasValue && notification.EpisodeMediaIds.Contains(review.MediaId.Value))
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            logger.LogInformation("SeriesDeletedEvent for Series {SeriesId}: no reviews to delete.", notification.SeriesId);
            return;
        }

        dbContext.Reviews.RemoveRange(reviews);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "SeriesDeletedEvent for Series {SeriesId}: deleted {Count} review(s).",
            notification.SeriesId,
            reviews.Count);
    }
}
