using MediatR;

namespace MediaRankerServer.Modules.Files.Events;

public record FileDeletedEvent(string FileKey, string EntityType) : INotification;
