using FluentAssertions;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.EventHandlers;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Reviews.EventHandlers;

public class MediaDeletedHandlerTests
{
    private PostgreSQLContext CreateContext() =>
        new(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_DeletesReviewsForMedia()
    {
        var context = CreateContext();
        context.Reviews.AddRange(
            new Review { Id = 1, UserId = "u1", MediaId = 10, TemplateId = 1, OverallScore = 7 },
            new Review { Id = 2, UserId = "u2", MediaId = 10, TemplateId = 1, OverallScore = 8 },
            new Review { Id = 3, UserId = "u1", MediaId = 99, TemplateId = 1, OverallScore = 5 }
        );
        await context.SaveChangesAsync();

        var handler = new MediaDeletedHandler(context, NullLogger<MediaDeletedHandler>.Instance);
        await handler.Handle(new MediaDeletedEvent(10), CancellationToken.None);

        context.Reviews.Where(r => r.MediaId == 10).Should().BeEmpty();
        context.Reviews.Where(r => r.MediaId == 99).Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenNoReviews_IsIdempotent()
    {
        var context = CreateContext();

        var handler = new MediaDeletedHandler(context, NullLogger<MediaDeletedHandler>.Instance);
        var act = () => handler.Handle(new MediaDeletedEvent(42), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
