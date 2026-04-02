using MediatR;
using MediaRankerServer.Modules.Templates.Events;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Modules.Reviews.EventHandlers;

public class TemplateDeletedHandler(
    PostgreSQLContext dbContext,
    ILogger<TemplateDeletedHandler> logger
) : INotificationHandler<TemplateDeletedEvent>
{
    public async Task Handle(TemplateDeletedEvent notification, CancellationToken cancellationToken)
    {
        var reviews = await dbContext.Reviews
            .Where(r => r.TemplateId == notification.TemplateId)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            logger.LogInformation(
                "TemplateDeletedEvent for Template {TemplateId}: no reviews to delete.",
                notification.TemplateId);
            return;
        }

        dbContext.Reviews.RemoveRange(reviews);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "TemplateDeletedEvent for Template {TemplateId}: deleted {Count} review(s).",
            notification.TemplateId,
            reviews.Count);
    }
}
