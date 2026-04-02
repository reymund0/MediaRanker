using FluentAssertions;
using MediaRankerServer.Modules.Templates.Events;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.EventHandlers;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Reviews.EventHandlers;

public class TemplateDeletedHandlerTests
{
    private PostgreSQLContext CreateContext() =>
        new(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_DeletesReviewsForTemplate()
    {
        var context = CreateContext();
        context.Reviews.AddRange(
            new Review { Id = 1, UserId = "u1", MediaId = 10, TemplateId = 5, OverallScore = 7 },
            new Review { Id = 2, UserId = "u2", MediaId = 20, TemplateId = 5, OverallScore = 8 },
            new Review { Id = 3, UserId = "u1", MediaId = 30, TemplateId = 99, OverallScore = 5 }
        );
        await context.SaveChangesAsync();

        var handler = new TemplateDeletedHandler(context, NullLogger<TemplateDeletedHandler>.Instance);
        await handler.Handle(new TemplateDeletedEvent(5), CancellationToken.None);

        context.Reviews.Where(r => r.TemplateId == 5).Should().BeEmpty();
        context.Reviews.Where(r => r.TemplateId == 99).Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenNoReviews_IsIdempotent()
    {
        var context = CreateContext();

        var handler = new TemplateDeletedHandler(context, NullLogger<TemplateDeletedHandler>.Instance);
        var act = () => handler.Handle(new TemplateDeletedEvent(42), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
