using MediatR;

namespace MediaRankerServer.Modules.Media.Events;

public sealed record SeriesDeletedEvent(long SeriesId, IReadOnlyCollection<long> EpisodeMediaIds) : INotification;
