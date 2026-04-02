using FluentAssertions;
using MediaRankerServer.Modules.Templates.Events;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Modules.Reviews.EventHandlers;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediaRankerServer.UnitTests.Modules.Reviews.EventHandlers;

public class TemplateFieldsDeletedHandlerTests
{
    private PostgreSQLContext CreateContext() =>
        new(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Handle_WhenNoAffectedReviews_IsIdempotent()
    {
        var context = CreateContext();

        var handler = new TemplateFieldsDeletedHandler(context, NullLogger<TemplateFieldsDeletedHandler>.Instance);
        var act = () => handler.Handle(new TemplateFieldsDeletedEvent(1, [100, 200]), CancellationToken.None);

        await act.Should().NotThrowAsync();
        context.ReviewFields.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DeletesReviewFieldsForDeletedTemplateFields()
    {
        var context = CreateContext();
        // Review with 2 fields; field 10 is deleted, field 20 stays.
        context.Reviews.Add(new Review { Id = 1, UserId = "u1", MediaId = 1, TemplateId = 1, OverallScore = 8 });
        await context.SaveChangesAsync();
        context.ReviewFields.AddRange(
            new ReviewField { ReviewId = 1, TemplateFieldId = 10, Value = 6 },
            new ReviewField { ReviewId = 1, TemplateFieldId = 20, Value = 10 }
        );
        await context.SaveChangesAsync();

        var handler = new TemplateFieldsDeletedHandler(context, NullLogger<TemplateFieldsDeletedHandler>.Instance);
        await handler.Handle(new TemplateFieldsDeletedEvent(1, [10]), CancellationToken.None);

        context.ReviewFields.Any(rf => rf.TemplateFieldId == 10).Should().BeFalse();
        context.ReviewFields.Any(rf => rf.TemplateFieldId == 20).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RecalculatesOverallScore_WhenFieldsRemain()
    {
        var context = CreateContext();
        context.Reviews.Add(new Review { Id = 1, UserId = "u1", MediaId = 1, TemplateId = 1, OverallScore = 8 });
        await context.SaveChangesAsync();
        // Fields: 10 (value 4, deleted) and 20 (value 6, stays). New avg = 6.
        context.ReviewFields.AddRange(
            new ReviewField { ReviewId = 1, TemplateFieldId = 10, Value = 4 },
            new ReviewField { ReviewId = 1, TemplateFieldId = 20, Value = 6 }
        );
        await context.SaveChangesAsync();

        var handler = new TemplateFieldsDeletedHandler(context, NullLogger<TemplateFieldsDeletedHandler>.Instance);
        await handler.Handle(new TemplateFieldsDeletedEvent(1, [10]), CancellationToken.None);

        var review = await context.Reviews.FindAsync(1L);
        review!.OverallScore.Should().Be(6);
    }

    [Fact]
    public async Task Handle_DeletesReview_WhenLastFieldRemoved()
    {
        var context = CreateContext();
        context.Reviews.Add(new Review { Id = 1, UserId = "u1", MediaId = 1, TemplateId = 1, OverallScore = 7 });
        await context.SaveChangesAsync();
        context.ReviewFields.Add(new ReviewField { ReviewId = 1, TemplateFieldId = 10, Value = 7 });
        await context.SaveChangesAsync();

        var handler = new TemplateFieldsDeletedHandler(context, NullLogger<TemplateFieldsDeletedHandler>.Instance);
        await handler.Handle(new TemplateFieldsDeletedEvent(1, [10]), CancellationToken.None);

        context.Reviews.Any(r => r.Id == 1).Should().BeFalse();
        context.ReviewFields.Any(rf => rf.ReviewId == 1).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HandlesMultipleAffectedReviews()
    {
        var context = CreateContext();
        context.Reviews.AddRange(
            new Review { Id = 1, UserId = "u1", MediaId = 1, TemplateId = 1, OverallScore = 5 },
            new Review { Id = 2, UserId = "u2", MediaId = 2, TemplateId = 1, OverallScore = 5 }
        );
        await context.SaveChangesAsync();
        // Both reviews share field 10 (deleted) and have field 20 (stays, value 8).
        context.ReviewFields.AddRange(
            new ReviewField { ReviewId = 1, TemplateFieldId = 10, Value = 2 },
            new ReviewField { ReviewId = 1, TemplateFieldId = 20, Value = 8 },
            new ReviewField { ReviewId = 2, TemplateFieldId = 10, Value = 4 },
            new ReviewField { ReviewId = 2, TemplateFieldId = 20, Value = 8 }
        );
        await context.SaveChangesAsync();

        var handler = new TemplateFieldsDeletedHandler(context, NullLogger<TemplateFieldsDeletedHandler>.Instance);
        await handler.Handle(new TemplateFieldsDeletedEvent(1, [10]), CancellationToken.None);

        var r1 = await context.Reviews.FindAsync(1L);
        var r2 = await context.Reviews.FindAsync(2L);
        r1!.OverallScore.Should().Be(8);
        r2!.OverallScore.Should().Be(8);
    }
}
