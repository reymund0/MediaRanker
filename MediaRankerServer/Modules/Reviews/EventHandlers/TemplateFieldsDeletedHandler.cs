using MediaRankerServer.Modules.Templates.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.Modules.Reviews.EventHandlers;

public class TemplateFieldsDeletedHandler(ILogger<TemplateFieldsDeletedHandler> logger) 
    : INotificationHandler<TemplateFieldsDeletedEvent>
{
    public Task Handle(TemplateFieldsDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Received TemplateFieldsDeletedEvent for Template {TemplateId}. Deleted Fields: {FieldIds}", 
            notification.TemplateId, 
            string.Join(", ", notification.FieldIds));

        // TODO: MR-15 recalculate OverallScore for Reviews associated with these field deletions
        
        return Task.CompletedTask;
    }
}
