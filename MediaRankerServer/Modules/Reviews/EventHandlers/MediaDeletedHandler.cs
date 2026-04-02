using MediatR;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Modules.Reviews.EventHandlers;

public class MediaDeletedHandler(
    PostgreSQLContext dbContext,
    ILogger<MediaDeletedHandler> logger
) : INotificationHandler<MediaDeletedEvent>
{
    public async Task Handle(MediaDeletedEvent notification, CancellationToken cancellationToken)
    {
        var reviews = await dbContext.Reviews
            .Where(r => r.MediaId == notification.MediaId)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            logger.LogInformation(
                "MediaDeletedEvent for Media {MediaId}: no reviews to delete.",
                notification.MediaId);
            return;
        }

        dbContext.Reviews.RemoveRange(reviews);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "MediaDeletedEvent for Media {MediaId}: deleted {Count} review(s).",
            notification.MediaId,
            reviews.Count);
    }
}
