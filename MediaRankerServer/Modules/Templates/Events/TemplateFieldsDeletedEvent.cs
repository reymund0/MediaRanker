using MediatR;

namespace MediaRankerServer.Modules.Templates.Events;

public record TemplateFieldsDeletedEvent(long TemplateId, List<long> FieldIds) : INotification;
