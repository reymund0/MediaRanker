using System;

namespace MediaRankerServer.Shared.Data.Interfaces;

public interface ITimestampedEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
