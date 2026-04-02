using MediatR;

namespace MediaRankerServer.Modules.Templates.Events;

public record TemplateDeletedEvent(long TemplateId) : INotification;
