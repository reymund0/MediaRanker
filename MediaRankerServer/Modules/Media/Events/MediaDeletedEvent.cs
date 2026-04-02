using MediatR;

namespace MediaRankerServer.Modules.Media.Events;

public record MediaDeletedEvent(long MediaId) : INotification;
