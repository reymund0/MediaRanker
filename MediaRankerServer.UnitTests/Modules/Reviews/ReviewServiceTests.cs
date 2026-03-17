using MediaRankerServer.Modules.Media.Services;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Templates.Services;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Shared.Data;
using Moq;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Modules.Reviews.Contracts;
using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Modules.Reviews.Services;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Templates.Entities;


namespace MediaRankerServer.UnitTests.Modules.Reviews;

public class ReviewServiceTests : IDisposable
{
    private readonly Mock<IValidator<ReviewUpsertRequest>> _mockValidator;
    private readonly Mock<IMediaService> _mockMediaService;
    private readonly Mock<ITemplateService> _mockTemplatesService;
    private readonly PostgreSQLContext _dbContext;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _mockValidator = new Mock<IValidator<ReviewUpsertRequest>>();
        _mockMediaService = new Mock<IMediaService>();
        _mockTemplatesService = new Mock<ITemplateService>();

        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PostgreSQLContext(options);

        _service = new ReviewService(
            _dbContext,
            _mockValidator.Object,
            _mockMediaService.Object,
            _mockTemplatesService.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedDataAsync()
    {
        var mediaType = new MediaType { Id = 1, Name = "Movie" };
        var media = new MediaEntity { Id = 1, Title = "Test Movie", MediaTypeId = 1, MediaType = mediaType };
        var template = new Template 
        { 
            Id = 1, 
            Name = "Test Template", 
            MediaTypeId = 1, 
            UserId = "user1",
            MediaType = mediaType,
            Fields = new List<TemplateField>
            {
                new() { Id = 1, Name = "Story" },
                new() { Id = 2, Name = "Acting" }
            }
        };

        _dbContext.MediaTypes.Add(mediaType);
        _dbContext.Media.Add(media);
        _dbContext.Templates.Add(template);
        await _dbContext.SaveChangesAsync();
    }

    private void SetupValidMocks()
    {
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ReviewUpsertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediaService.Setup(m => m.GetMediaByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaDto { Id = 1, Title = "Test Movie", MediaType = new MediaRankerServer.Modules.Media.Contracts.MediaTypeDto { Id = 1, Name = "Movie" }, ReleaseDate = new DateOnly(2020, 1, 1) });

        _mockTemplatesService.Setup(t => t.GetTemplateByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateDto { 
                Id = 1, 
                Name = "Test Template", 
                MediaType = new MediaRankerServer.Modules.Media.Contracts.MediaTypeDto { Id = 1, Name = "Movie" },
                Fields = [
                    new TemplateFieldDto { Id = 1, Name = "Story", Position = 1 },
                    new TemplateFieldDto { Id = 2, Name = "Acting", Position = 2 }
                ]
            });
    }

    [Fact]
    public async Task ValidateReviewUpsertRequest_WhenMediaIdIsInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        
        var request = new ReviewUpsertRequest
        {
            MediaId = -10,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        _mockMediaService.Setup(m => m.GetMediaByIdAsync(-10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaDto?)null);

        // Act & Assert
        var act = () => _service.CreateReviewAsync("test-user", request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_upsert_validation_error")
            .Where(e => e.Message.Contains("MediaId"));
    }

    [Fact]
    public async Task ValidateReviewUpsertRequest_WhenTemplateIdIsInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = -10,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        _mockTemplatesService.Setup(t => t.GetTemplateByIdAsync(-10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateDto?)null);

        // Act & Assert
        var act = () => _service.CreateReviewAsync("test-user", request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_upsert_validation_error")
            .Where(e => e.Message.Contains("TemplateId"));
    }

    [Fact]
    public async Task ValidateReviewUpsertRequest_WhenTemplateFieldsAreInvalid_ThrowsValidationDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 999, Value = 5 } }
        };

        // Act & Assert
        var act = () => _service.CreateReviewAsync("test-user", request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_upsert_validation_error")
            .Where(e => e.Message.Contains("Template field"));
    }

    [Fact]
    public async Task ValidateReviewUpsertRequest_WhenMediaTypeIsIncompatible_ThrowsValidationDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        _mockMediaService.Setup(m => m.GetMediaByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaDto { Id = 1, Title = "Test", MediaType = new MediaRankerServer.Modules.Media.Contracts.MediaTypeDto { Id = 2, Name = "Book" } });

        // Act & Assert
        var act = () => _service.CreateReviewAsync("test-user", request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_upsert_validation_error")
            .Where(e => e.Message.Contains("Media type"));
    }

    [Fact]
    public async Task CreateReviewAsync_WhenUserAlreadyHasReviewForMedia_ThrowsDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        var existingReview = new Review
        {
            UserId = "test-user",
            MediaId = 1,
            TemplateId = 1,
            OverallScore = 5
        };
        _dbContext.Reviews.Add(existingReview);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _service.CreateReviewAsync("test-user", request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_duplicate_review");
    }

    [Fact]
    public async Task ValidateUpdateReviewAsync_WhenReviewsNotFound_ThrowsDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var reviewId = 999;
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        // Act & Assert
        var act = () => _service.UpdateReviewAsync("test-user", reviewId, request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_not_found");
    }

    [Fact]
    public async Task ValidateUpdateReviewAsync_WhenReviewsDoesNotBelongToUser_ThrowsDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var review = new Review
        {
            UserId = "other-user",
            MediaId = 1,
            TemplateId = 1,
            OverallScore = 5
        };
        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            Scores = new List<ReviewFieldUpsertRequest> { new() { TemplateFieldId = 1, Value = 5 } }
        };

        // Act & Assert
        var act = () => _service.UpdateReviewAsync("test-user", review.Id, request);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_forbidden");
    }

    [Fact]
    public async Task ValidateDeleteReviewAsync_WhenReviewsNotFound_ThrowsDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var reviewId = 999;

        // Act & Assert
        var act = () => _service.DeleteReviewAsync("test-user", reviewId);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_not_found");
    }

    [Fact]
    public async Task ValidateDeleteReviewAsync_WhenReviewsDoesNotBelongToUser_ThrowsDomainException()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var review = new Review
        {
            UserId = "other-user",
            MediaId = 1,
            TemplateId = 1,
            OverallScore = 5
        };
        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var act = () => _service.DeleteReviewAsync("test-user", review.Id);
        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Type == "reviews_forbidden");
    }
    
    [Fact]
    public async Task CreateReviewAsync_ValidRequest_CreatesAndReturnsDto()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var userId = "user1";
        var request = new ReviewUpsertRequest
        {
            MediaId = 1,
            TemplateId = 1,
            ReviewTitle = "Great Movie",
            Scores = new List<ReviewFieldUpsertRequest>
            {
                new() { TemplateFieldId = 1, Value = 8 },
                new() { TemplateFieldId = 2, Value = 9 }
            }
        };

        // Act
        var result = await _service.CreateReviewAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.MediaId.Should().Be(request.MediaId);
        result.TemplateId.Should().Be(request.TemplateId);
        result.ReviewTitle.Should().Be(request.ReviewTitle);
        result.OverallScore.Should().Be(8); // (8+9)/2 = 8.5 -> 8 (Banker's rounding)
        result.Scores.Should().HaveCount(2);

        var savedReview = await _dbContext.Reviews.Include(rm => rm.Scores).FirstAsync();
        savedReview.Should().NotBeNull();
        savedReview.Scores.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateReviewAsync_ValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var userId = "user1";
        
        var mediaEntity = await _dbContext.Media.FirstAsync();
        var templateEntity = await _dbContext.Templates.FirstAsync();
        
        var existingReview = new Review
        {
            UserId = userId,
            MediaId = 1,
            TemplateId = 1,
            ReviewTitle = "Old Title",
            OverallScore = 5,
            Media = mediaEntity,
            Template = templateEntity,
            Scores = new List<ReviewField>
            {
                new() { TemplateFieldId = 1, Value = 5 }
            }
        };
        _dbContext.Reviews.Add(existingReview);
        await _dbContext.SaveChangesAsync();

        var request = new ReviewUpsertRequest
        {
            Id = existingReview.Id,
            MediaId = 1,
            TemplateId = 1,
            ReviewTitle = "New Title",
            Scores = new List<ReviewFieldUpsertRequest>
            {
                new() { TemplateFieldId = 1, Value = 8 }, // Update
                new() { TemplateFieldId = 2, Value = 9 }  // Add
            }
        };

        // Act
        var result = await _service.UpdateReviewAsync(userId, existingReview.Id, request);

        // Assert
        result.Should().NotBeNull();
        result.ReviewTitle.Should().Be("New Title");
        result.OverallScore.Should().Be(8); // (8+9)/2 = 8.5 -> 8 (Banker's rounding)
        result.Scores.Should().HaveCount(2);

        var updatedReview = await _dbContext.Reviews.Include(rm => rm.Scores).FirstAsync();
        updatedReview.ReviewTitle.Should().Be("New Title");
        updatedReview.Scores.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task DeleteReviewAsync_ValidId_DeletesReviewAndScores()
    {
        // Arrange
        await SeedDataAsync();
        SetupValidMocks();
        var userId = "user1";
        
        var existingReview = new Review
        {
            UserId = userId,
            MediaId = 1,
            TemplateId = 1,
            OverallScore = 5,
            Scores = new List<ReviewField>
            {
                new() { TemplateFieldId = 1, Value = 5 }
            }
        };
        _dbContext.Reviews.Add(existingReview);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteReviewAsync(userId, existingReview.Id);

        // Assert
        var reviewExists = await _dbContext.Reviews.AnyAsync();
        reviewExists.Should().BeFalse();
        
        var scoresExist = await _dbContext.ReviewFields.AnyAsync();
        scoresExist.Should().BeFalse();
    }
}
